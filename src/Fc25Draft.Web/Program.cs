using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Repositories;
using Fc25Draft.Web.Extensions;
using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<DraftDbContext>(opt =>
    opt.UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(DraftDbContext).Assembly.FullName))
       .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
       .EnableDetailedErrors(builder.Environment.IsDevelopment())
);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddScoped<DraftService>();
builder.Services.AddScoped<DraftStateService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IPositionService, PositionService>();

var app = builder.Build();

await app.SeedDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapHub<DraftHub>("/hubs/draft");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.MapPost("/api/draft/reset", async (DraftService draftService, CancellationToken ct) =>
{
    await draftService.ResetDraftAsync(ct);
    return Results.NoContent();
});

var draftApi = app.MapGroup("/api/draft");

draftApi.MapGet("/state", async (DraftStateService draftStateService, CancellationToken ct) =>
{
    var state = await draftStateService.GetStateAsync(ct);
    return Results.Ok(state);
});

draftApi.MapPost("/pick", async (DraftStateService draftStateService, DraftPickRequestDto request, CancellationToken ct) =>
{
    try
    {
        var state = await draftStateService.MakePickAsync(request.PlayerId, request.Token, ct);
        return Results.Ok(state);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

draftApi.MapPost("/generate", async (
    DraftService draftService,
    DraftStateService draftStateService,
    IHubContext<DraftHub> hubContext,
    GenerateDraftRequestDto request,
    CancellationToken ct) =>
{
    if (request is null)
    {
        return Results.BadRequest(new { message = "Requisição inválida." });
    }

    if (request.TotalRounds is < 1 or > 50)
    {
        return Results.BadRequest(new { message = "O número de rodadas deve estar entre 1 e 50." });
    }

    try
    {
        await draftService.GenerateDraftAsync(request.TotalRounds, request.Snake, ct);
        var state = await draftStateService.GetStateAsync(ct);
        await hubContext.Clients.All.SendAsync("DraftAtualizado", cancellationToken: ct);
        return Results.Ok(state);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.Run();
