using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Endpoints.Market;
using Fc25Draft.Web.Extensions.Endpoints;
using Fc25Draft.Web.Extensions;
using Fc25Draft.Web.Extensions.DI;
using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Security;
using Fc25Draft.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Blazored.Toast;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddInfra(builder.Configuration, builder.Environment)
    .AddCoreServices(builder.Configuration)
    .AddMarketHistoryFeature();

builder.Services
    .AddAuthentication(opts => {
        opts.DefaultAuthenticateScheme = AdminTokenAuthenticationHandler.SchemeName;
        opts.DefaultChallengeScheme = AdminTokenAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, AdminTokenAuthenticationHandler>(
        AdminTokenAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", p => p.RequireRole("Admin")));
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(o => o.DetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddBlazoredToast();
builder.Services.AddSingleton<IIdempotencyStore, PostgresIdempotencyStore>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
    await db.Database.MigrateAsync();

    // Cria tabelas de loteria se ainda não existirem (sem migration)
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "LigaLoterias" (
            "LoteraiaId"  uuid        NOT NULL,
            "LigaId"      uuid        NOT NULL,
            "LigaNome"    text        NOT NULL,
            "IsFinished"  boolean     NOT NULL DEFAULT false,
            "CriadoEm"   timestamptz NOT NULL,
            CONSTRAINT "PK_LigaLoterias" PRIMARY KEY ("LoteraiaId")
        );
        CREATE TABLE IF NOT EXISTS "LigaLoteriaPicks" (
            "PickId"      uuid    NOT NULL,
            "LoteraiaId"  uuid    NOT NULL,
            "Rodada"      integer NOT NULL,
            "PickNumero"  integer NOT NULL,
            "TimeId"      uuid    NOT NULL,
            "TimeNome"    text    NOT NULL,
            "Posicao"     integer NOT NULL,
            CONSTRAINT "PK_LigaLoteriaPicks"   PRIMARY KEY ("PickId"),
            CONSTRAINT "FK_LigaLoteriaPicks_LigaLoterias"
                FOREIGN KEY ("LoteraiaId") REFERENCES "LigaLoterias"("LoteraiaId") ON DELETE CASCADE
        );

        ALTER TABLE "TransferHistories" ADD COLUMN IF NOT EXISTS "CycleId" uuid NULL;

        -- Backfill CycleId para contratações via mercado (MarketAuction = 1) existentes
        UPDATE "TransferHistories" th
        SET "CycleId" = mi."CycleId"
        FROM "MarketTransactions" mt
        JOIN "MarketItems" mi ON mt."ItemId" = mi."ItemId"
        WHERE th."CycleId" IS NULL
          AND th."Type" = 1
          AND th."PlayerId" = mi."PlayerId"
          AND mt."Type" IN (3, 5);

        ALTER TABLE "Ligas" ADD COLUMN IF NOT EXISTS "Tipo" integer NOT NULL DEFAULT 0;
        ALTER TABLE "LigaClassificacoes" ADD COLUMN IF NOT EXISTS "Grupo" integer NULL;

        CREATE TABLE IF NOT EXISTS "LigaGruposTimes" (
            "Id"      uuid    NOT NULL,
            "LigaId"  uuid    NOT NULL,
            "TimeId"  uuid    NOT NULL,
            "Grupo"   integer NOT NULL,
            CONSTRAINT "PK_LigaGruposTimes" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_LigaGruposTimes_Ligas"
                FOREIGN KEY ("LigaId") REFERENCES "Ligas"("LigaId") ON DELETE CASCADE,
            CONSTRAINT "FK_LigaGruposTimes_Teams"
                FOREIGN KEY ("TimeId") REFERENCES "Teams"("TeamId") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_LigaGruposTimes_LigaId_TimeId"
            ON "LigaGruposTimes"("LigaId", "TimeId");
        CREATE INDEX IF NOT EXISTS "IX_LigaGruposTimes_LigaId_Grupo"
            ON "LigaGruposTimes"("LigaId", "Grupo");
        """);

    // Restaura última loteria finalizada no serviço singleton
    var lotteryService = app.Services.GetRequiredService<LotteryStateService>();
    var lastLoteria = await db.LigaLoterias
        .Include(x => x.Picks)
        .Where(x => x.IsFinished)
        .OrderByDescending(x => x.CriadoEm)
        .FirstOrDefaultAsync();

    if (lastLoteria is not null)
    {
        var r1 = lastLoteria.Picks.Where(p => p.Rodada == 1).OrderBy(p => p.PickNumero).ToList();
        var r2 = lastLoteria.Picks.Where(p => p.Rodada == 2).OrderBy(p => p.PickNumero).ToList();
        var rodada = r2.Count > 0 ? 2 : 1;

        var state = new LotteryState
        {
            LigaId   = lastLoteria.LigaId,
            LigaNome = lastLoteria.LigaNome,
            Rodada   = rodada,
            IsFinished = true,
        };

        var revealed = rodada == 2 ? r2 : r1;
        state.Revealed.AddRange(revealed.Select(p =>
            new LotteryPickItem(p.PickNumero, p.TimeId, p.TimeNome, p.Posicao)));

        if (r1.Count > 0 && rodada == 2)
            state.Round1Results = r1.Select(p =>
                new LotteryPickItem(p.PickNumero, p.TimeId, p.TimeNome, p.Posicao)).ToList();

        lotteryService.Start(state);
    }
}
if (!app.Environment.IsEnvironment("Testing"))
{
    await app.SeedDatabaseAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<DraftHub>("/hubs/draft");
app.MapHub<MarketHub>("/hubs/market");

var api = app.MapGroup("/api");
api.MapDraftEndpoints()
   .MapPlayerEndpoints()
   .MapTeamEndpoints()
   .MapBudgetEndpoints()
   .MapPricingEndpoints()
   .MapTransfersEndpoints()
   .MapTransferOffersEndpoints()
   .MapMarketEndpoints()
   .MapMarketHistoryEndpoints()
   .MapAdminEndpoints()
   .MapLigaEndpoints();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapHealthChecks("/health");

app.Run();
