using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Repositories;
using Fc25Draft.Infra.Services;
using Fc25Draft.Web.Extensions;
using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Options;
using Fc25Draft.Web.Security;
using Fc25Draft.Web.Services;
using HealthChecks.NpgSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
var connectionString = ResolveConnectionString(builder);

builder.Services.AddDbContext<DraftDbContext>(options =>
    options
        .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(DraftDbContext).Assembly.FullName))
        .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
        .EnableDetailedErrors(builder.Environment.IsDevelopment()));

builder.Services.AddHealthChecks().AddNpgSql(connectionString);

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.Configure<PricingOptions>(builder.Configuration.GetSection(PricingOptions.SectionName));
builder.Services.Configure<MarketOptions>(builder.Configuration.GetSection(MarketOptions.SectionName));
builder.Services.Configure<EconomiaOptions>(builder.Configuration.GetSection(EconomiaOptions.SectionName));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = AdminTokenAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = AdminTokenAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, AdminTokenAuthenticationHandler>(
        AdminTokenAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddScoped<DraftService>();
builder.Services.AddScoped<DraftStateService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<ApiClientFactory>();
builder.Services.AddScoped<PlayersApiClient>();
builder.Services.AddScoped<DraftAdminApiClient>();
builder.Services.AddScoped<TeamsApiClient>();
builder.Services.AddScoped<AdminTransfersApiClient>();
builder.Services.AddScoped<BudgetsApiClient>();
builder.Services.AddScoped<MarketApiClient>();
builder.Services.AddScoped<MarketClient>();
builder.Services.AddScoped<MarketHubClient>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IMarketCycleGenerator, MarketCycleGenerator>();
builder.Services.AddScoped<IMarketService, MarketService>();
builder.Services.AddScoped<IMarketItemPublicationService, MarketItemPublicationService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<AdminTransferService>();
builder.Services.AddScoped<ITransfersQueryService, TransfersQueryService>();
builder.Services.AddScoped<ITransferHistoryService, TransferHistoryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
    await db.Database.MigrateAsync();
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
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapHealthChecks("/health");

var api = app.MapGroup("/api");

MapAdminEndpoints(api);
MapDraftEndpoints(api);
    MapPlayerEndpoints(api);
    MapTeamEndpoints(api);
    MapPricingEndpoints(api);
    MapMarketEndpoints(api);
MapTransfersEndpoints(api);
MapBudgetEndpoints(api);

app.Run();

static string ResolveConnectionString(WebApplicationBuilder builder)
{
    var rawConnectionString =
        Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Unable to resolve PostgreSQL connection string.");

    NpgsqlConnectionStringBuilder connectionBuilder;

    if (rawConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(rawConnectionString);
        var userInfo = uri.UserInfo.Split(':');

        connectionBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.Trim('/'),
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? userInfo[1] : string.Empty,
            SslMode = SslMode.Require
        };
    }
    else
    {
        connectionBuilder = new NpgsqlConnectionStringBuilder(rawConnectionString);
    }

    if (!builder.Environment.IsDevelopment())
    {
        var host = connectionBuilder.Host;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Localhost database connections are not allowed outside Development.");
        }
    }

    return connectionBuilder.ToString();
}

static void MapAdminEndpoints(RouteGroupBuilder api)
{
    var adminApi = api.MapGroup("/admin").RequireAuthorization("AdminOnly");

    adminApi.MapGet("/validate", () => Results.Ok(new { status = "ok" }));

    var adminTransferApi = adminApi.MapGroup("/transfer");

    adminTransferApi.MapPost("/sell", async (
        HttpContext httpContext,
        AdminSellPlayersRequestDto request,
        AdminTransferService adminTransferService,
        CancellationToken ct) =>
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Payload inválido." });
        }

        if (request.FromTeamId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Time de origem é obrigatório." });
        }

        if (request.ToTeamId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Time de destino é obrigatório." });
        }

        if (request.FromTeamId == request.ToTeamId)
        {
            return Results.BadRequest(new { message = "Informe times diferentes para a venda." });
        }

        if (request.PlayerIds is null || request.PlayerIds.Length == 0)
        {
            return Results.BadRequest(new { message = "Selecione ao menos um jogador." });
        }

        if (request.PlayerIds.Any(id => id == Guid.Empty))
        {
            return Results.BadRequest(new { message = "Jogador inválido na lista." });
        }

        if (request.PlayerIds.Distinct().Count() != request.PlayerIds.Length)
        {
            return Results.BadRequest(new { message = "Não é permitido repetir jogadores." });
        }

        if (request.Amount < 0m)
        {
            return Results.BadRequest(new { message = "O valor não pode ser negativo." });
        }

        if (!TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            await adminTransferService.SellAsync(
                adminToken!,
                request.FromTeamId,
                request.ToTeamId,
                request.PlayerIds,
                request.Amount,
                request.Reason,
                ct);

            return Results.Ok(new { message = "Venda concluída com sucesso." });
        }
        catch (AdminForbiddenException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (AdminConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    });

    adminTransferApi.MapPost("/swap", async (
        HttpContext httpContext,
        AdminSwapPlayersRequestDto request,
        AdminTransferService adminTransferService,
        CancellationToken ct) =>
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Payload inválido." });
        }

        if (request.TeamAId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Time A é obrigatório." });
        }

        if (request.TeamBId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Time B é obrigatório." });
        }

        if (request.TeamAId == request.TeamBId)
        {
            return Results.BadRequest(new { message = "Informe times diferentes para a troca." });
        }

        var playersFromA = request.PlayersFromA ?? Array.Empty<Guid>();
        var playersFromB = request.PlayersFromB ?? Array.Empty<Guid>();

        if (playersFromA.Length == 0 && playersFromB.Length == 0)
        {
            return Results.BadRequest(new { message = "Selecione ao menos um jogador para a troca." });
        }

        if (playersFromA.Any(id => id == Guid.Empty))
        {
            return Results.BadRequest(new { message = "Jogador inválido na lista do Time A." });
        }

        if (playersFromB.Any(id => id == Guid.Empty))
        {
            return Results.BadRequest(new { message = "Jogador inválido na lista do Time B." });
        }

        if (playersFromA.Distinct().Count() != playersFromA.Length)
        {
            return Results.BadRequest(new { message = "Não é permitido repetir jogadores do Time A." });
        }

        if (playersFromB.Distinct().Count() != playersFromB.Length)
        {
            return Results.BadRequest(new { message = "Não é permitido repetir jogadores do Time B." });
        }

        if (playersFromA.Intersect(playersFromB).Any())
        {
            return Results.BadRequest(new { message = "Um jogador não pode participar pelos dois times." });
        }

        if (!TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            await adminTransferService.SwapAsync(
                adminToken!,
                request.TeamAId,
                playersFromA,
                request.TeamBId,
                playersFromB,
                request.CashAdjustFromAToB,
                request.Reason,
                ct);

            return Results.Ok(new { message = "Troca concluída com sucesso." });
        }
        catch (AdminForbiddenException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (AdminConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    });

    adminTransferApi.MapPost("/move", async (
        HttpContext httpContext,
        AdminMovePlayerRequestDto request,
        AdminTransferService adminTransferService,
        CancellationToken ct) =>
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Payload inválido." });
        }

        if (request.PlayerId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Jogador é obrigatório." });
        }

        if (request.ToTeamId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Time de destino é obrigatório." });
        }

        if (!TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            await adminTransferService.MoveAsync(
                adminToken!,
                request.PlayerId,
                request.ToTeamId,
                request.Reason,
                ct);

            return Results.Ok(new { message = "Movimentação concluída com sucesso." });
        }
        catch (AdminForbiddenException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (AdminConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    });
}

static void MapDraftEndpoints(RouteGroupBuilder api)
{
    var draftApi = api.MapGroup("/draft");

    draftApi.MapGet("/state", async (DraftStateService draftStateService, CancellationToken ct) =>
    {
        var state = await draftStateService.GetStateAsync(ct);
        return Results.Ok(state);
    });

    draftApi.MapGet("/board", async (DraftDbContext db, CancellationToken ct) =>
    {
        var draft = await db.Drafts
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (draft is null)
        {
            return Results.Ok(Array.Empty<DraftBoardEntryDto>());
        }

        var board = await db.DraftPicks
            .AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId)
            .OrderBy(p => p.OverallPick)
            .Select(p => new DraftBoardEntryDto(
                p.DraftId,
                p.RoundNumber,
                p.PickInRound,
                p.OverallPick,
                p.TeamId,
                p.Team.TeamName,
                p.Team.OwnerName,
                p.PlayerId,
                p.Player != null ? p.Player.Name : null,
                p.Player != null ? (short?)p.Player.PositionId : null,
                p.Player != null ? p.Player.Position.Name : null,
                p.PickedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(board);
    });

    draftApi.MapPost("/pick", async (DraftStateService draftStateService, DraftPickRequestDto request, CancellationToken ct) =>
    {
        try
        {
            var result = await draftStateService.MakePickAsync(request.PlayerId, request.Token, ct);
            return Results.Ok(result);
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

    draftApi.MapGet("/export/board", async (DraftDbContext db, CancellationToken ct) =>
    {
        var draft = await db.Drafts
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (draft is null)
        {
            var emptyCsv = BuildDraftBoardCsv(Array.Empty<DraftBoardExportDto>());
            return Results.File(Encoding.UTF8.GetBytes(emptyCsv), "text/csv", "draft-board.csv");
        }

        var board = await db.DraftPicks
            .AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId)
            .OrderBy(p => p.OverallPick)
            .Select(p => new DraftBoardExportDto(
                p.RoundNumber,
                p.PickInRound,
                p.Team.TeamName,
                p.Team.OwnerName,
                p.Player != null ? p.Player.Name : string.Empty,
                p.Player != null ? p.Player.Position.Name : string.Empty,
                p.PickedAtUtc.HasValue ? p.PickedAtUtc.Value.ToString("u") : string.Empty))
            .ToListAsync(ct);

        var csv = BuildDraftBoardCsv(board);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return Results.File(bytes, "text/csv", "draft-board.csv");
    });

    var adminDraftApi = api.MapGroup("/admin/draft");

    adminDraftApi.MapGet(string.Empty, async (DraftDbContext db, CancellationToken ct) =>
    {
        var drafts = await db.Drafts
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new DraftSummaryDto(
                d.DraftId,
                d.Name,
                d.TotalRounds,
                d.TotalTeams,
                d.CreatedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(drafts);
    }).AllowAnonymous();

    adminDraftApi.MapGet("/{id:guid}", async (DraftDbContext db, Guid id, CancellationToken ct) =>
    {
        var draft = await db.Drafts
            .AsNoTracking()
            .Where(d => d.DraftId == id)
            .Select(d => new DraftDetailsDto(
                d.DraftId,
                d.Name,
                d.TotalRounds,
                d.TotalTeams,
                d.CreatedAtUtc,
                d.Rounds
                    .OrderBy(r => r.RoundNumber)
                    .Select(r => new DraftRoundDetailsDto(
                        r.RoundNumber,
                        r.OverallMin,
                        r.OverallMax,
                        r.Picks
                            .OrderBy(p => p.PickInRound)
                            .Select(p => new DraftRoundPickDto(
                                p.PickInRound,
                                p.OverallPick,
                                p.TeamId,
                                p.Team.TeamName,
                                p.Team.OwnerName,
                                p.PlayerId,
                                p.Player != null ? p.Player.Name : null,
                                p.PickedAtUtc))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return draft is null ? Results.NotFound() : Results.Ok(draft);
    }).AllowAnonymous();

    var adminDraftProtectedApi = adminDraftApi.RequireAuthorization("AdminOnly");

    adminDraftProtectedApi.MapPost("/generate", async (
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
            IReadOnlyDictionary<int, (int? OverallMin, int? OverallMax)>? roundRules = null;
            if (request.RoundRules is { Count: > 0 })
            {
                var rules = new Dictionary<int, (int? OverallMin, int? OverallMax)>();
                foreach (var rule in request.RoundRules)
                {
                    rules[rule.Round] = (rule.OverallMin, rule.OverallMax);
                }

                roundRules = rules;
            }

            await draftService.GenerateDraftAsync(request.TotalRounds, request.Snake, roundRules, request.Name, ct);
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

    adminDraftProtectedApi.MapPost("/{id:guid}/rounds", async (
        DraftService draftService,
        DraftStateService draftStateService,
        IHubContext<DraftHub> hubContext,
        Guid id,
        DraftRoundCreateDto? request,
        CancellationToken ct) =>
    {
        try
        {
            request ??= new DraftRoundCreateDto(null, null);
            var round = await draftService.AddRoundAsync(id, request.OverallMin, request.OverallMax, ct);
            await draftStateService.GetStateAsync(ct);
            await hubContext.Clients.All.SendAsync("DraftAtualizado", cancellationToken: ct);
            return Results.Ok(round);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
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

    adminDraftProtectedApi.MapDelete("/{id:guid}/rounds/{roundNumber:int}", async (
        DraftService draftService,
        DraftStateService draftStateService,
        IHubContext<DraftHub> hubContext,
        Guid id,
        int roundNumber,
        CancellationToken ct) =>
    {
        try
        {
            await draftService.RemoveRoundAsync(id, roundNumber, ct);
            await draftStateService.GetStateAsync(ct);
            await hubContext.Clients.All.SendAsync("DraftAtualizado", cancellationToken: ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    });
}

static void MapPlayerEndpoints(RouteGroupBuilder api)
{
    var playersApi = api.MapGroup("/players");

    playersApi.MapGet(string.Empty, async (
        DraftDbContext db,
        string? q,
        [FromQuery(Name = "pos")] short[]? pos,
        bool? onlyAvailable,
        int? overallMin,
        int? overallMax,
        string? sortBy,
        string? sortOrder,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default) =>
    {
        var currentPage = Math.Max(1, page);
        var currentPageSize = pageSize <= 0 ? 10 : pageSize;

        var query = db.Players
            .AsNoTracking()
            .Where(p => true);

        if (overallMin.HasValue && overallMax.HasValue && overallMin > overallMax)
        {
            return Results.BadRequest(new { message = "Overall mínimo não pode ser maior que o máximo." });
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(
                EF.Functions.Unaccent(p.Name),
                EF.Functions.Unaccent(pattern)));
        }

        if (pos is { Length: > 0 })
        {
            var positions = pos.Distinct().ToArray();
            query = query.Where(p => positions.Contains(p.PositionId));
        }

        if (onlyAvailable is true)
        {
            query = query.Where(p => !p.TeamRosters.Any());
        }

        if (overallMin.HasValue)
        {
            query = query.Where(p => p.Overall >= overallMin.Value);
        }

        if (overallMax.HasValue)
        {
            query = query.Where(p => p.Overall <= overallMax.Value);
        }

        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy)
            ? "overall"
            : sortBy.Trim().ToLowerInvariant();
        var normalizedSortOrder = string.IsNullOrWhiteSpace(sortOrder)
            ? "desc"
            : sortOrder.Trim().ToLowerInvariant();
        var sortDescending = normalizedSortOrder != "asc";

        IOrderedQueryable<Player> orderedQuery = normalizedSortBy switch
        {
            "age" when sortDescending => query.OrderByDescending(p => p.Age ?? int.MinValue),
            "age" => query.OrderBy(p => p.Age ?? int.MaxValue),
            "overall" when sortDescending => query.OrderByDescending(p => p.Overall),
            "overall" => query.OrderBy(p => p.Overall),
            _ when sortDescending => query.OrderByDescending(p => p.Overall),
            _ => query.OrderBy(p => p.Overall)
        };

        orderedQuery = orderedQuery.ThenBy(p => p.Name);

        var total = await query.CountAsync(ct);

        var items = await orderedQuery
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .Select(p => new PlayerListItemDto(
                p.PlayerId,
                p.Name,
                p.PositionId,
                p.Position.Name,
                p.Overall,
                p.Age,
                p.TeamRosters.Any() ? "Escolhido" : "Disponível",
                p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault()))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<PlayerListItemDto>(items, total));
    });

    playersApi.MapGet("/{id:int}", async (DraftDbContext db, int id, CancellationToken ct = default) =>
    {
        var player = await db.Players
            .AsNoTracking()
            .Where(p => p.PlayerId == id)
            .Select(p => new PlayerDetailsDto(
                p.PlayerId,
                p.Name,
                p.PositionId,
                p.Position.Name,
                p.Overall,
                p.Age,
                p.TeamRosters.Any() ? "Escolhido" : "Disponível",
                p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                p.TeamRosters.Select(r => (Guid?)r.TeamId).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);

        return player is null ? Results.NotFound() : Results.Ok(player);
    });

    playersApi.MapGet("/export/csv", async (DraftDbContext db, CancellationToken ct) =>
    {
        var players = await LoadPlayerExportAsync(db, ct);
        var csv = BuildPlayerCsv(players);
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "jogadores.csv");
    });

    playersApi.MapGet("/export/json", async (DraftDbContext db, CancellationToken ct) =>
    {
        var players = await LoadPlayerExportAsync(db, ct);
        var json = JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true });
        return Results.File(Encoding.UTF8.GetBytes(json), "application/json", "jogadores.json");
    });

    playersApi.MapGet("/export/xlsx", async (DraftDbContext db, CancellationToken ct) =>
    {
        var players = await LoadPlayerExportAsync(db, ct);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Jogadores");
        worksheet.Cell(1, 1).Value = "Nome";
        worksheet.Cell(1, 2).Value = "Posição";
        worksheet.Cell(1, 3).Value = "Overall";
        worksheet.Cell(1, 4).Value = "Status";
        worksheet.Cell(1, 5).Value = "Time";

        for (var i = 0; i < players.Count; i++)
        {
            var row = i + 2;
            var player = players[i];
            worksheet.Cell(row, 1).Value = player.Nome;
            worksheet.Cell(row, 2).Value = player.Posicao;
            worksheet.Cell(row, 3).Value = player.Overall;
            worksheet.Cell(row, 4).Value = player.Status;
            worksheet.Cell(row, 5).Value = player.Time ?? string.Empty;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var bytes = stream.ToArray();
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "jogadores.xlsx");
    });

    var adminPlayersApi = api.MapGroup("/admin/players").RequireAuthorization("AdminOnly");

    adminPlayersApi.MapPost(string.Empty, async (IPlayerService playerService, PlayerCreateDto dto) =>
    {
        try
        {
            var id = await playerService.CreateAsync(dto);
            return Results.Created($"/api/players/{id}", new { id });
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

    adminPlayersApi.MapPut("/{id:int}", async (IPlayerService playerService, int id, PlayerUpdateDto dto) =>
    {
        try
        {
            await playerService.UpdateAsync(id, dto);
            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    });

    adminPlayersApi.MapDelete("/{id:int}", async (IPlayerService playerService, int id) =>
    {
        try
        {
            await playerService.DeleteAsync(id);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    });

    adminPlayersApi.MapPost("/import", async (HttpRequest request, IPlayerService playerService, CancellationToken ct) =>
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Envie um arquivo CSV válido." });
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "Arquivo CSV não encontrado." });
        }

        const long maxCsvSize = 5 * 1024 * 1024;
        if (file.Length > maxCsvSize)
        {
            return Results.BadRequest(new { message = "O arquivo deve ter no máximo 5 MB." });
        }

        await using var stream = file.OpenReadStream();
        var result = await playerService.ImportCsvAsync(stream, ct);
        return Results.Ok(result);
    });
}

static void MapTeamEndpoints(RouteGroupBuilder api)
{
    var teamsApi = api.MapGroup("/teams");

    teamsApi.MapGet(string.Empty, async (
        DraftDbContext db,
        string? q,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default) =>
    {
        var currentPage = Math.Max(1, page);
        var currentPageSize = pageSize <= 0 ? 10 : pageSize;

        var query = db.Teams.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(t => EF.Functions.Like(t.TeamName, pattern) ||
                                     (t.OwnerName != null && EF.Functions.Like(t.OwnerName, pattern)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(t => t.TeamName)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .Select(t => new TeamListItemDto(
                t.TeamId,
                t.TeamName,
                t.OwnerName,
                t.Roster.Count))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<TeamListItemDto>(items, total));
    });

    teamsApi.MapGet("/{id:guid}", async (DraftDbContext db, Guid id, HttpContext httpContext, CancellationToken ct = default) =>
    {
        var team = await db.Teams
            .AsNoTracking()
            .Where(t => t.TeamId == id)
            .Select(t => new
            {
                t.TeamId,
                t.TeamName,
                t.OwnerName,
                t.Token,
                Jogadores = t.Roster.Count
            })
            .FirstOrDefaultAsync(ct);

        if (team is null)
        {
            return Results.NotFound();
        }

        var includeToken = httpContext.User.IsInRole("Admin");
        var teamToken = includeToken ? team.Token : string.Empty;

        var dto = new TeamDetailsDto(
            team.TeamId,
            team.TeamName,
            team.OwnerName,
            teamToken,
            team.Jogadores);

        return Results.Ok(dto);
    });

    teamsApi.MapGet("/roster", async (DraftDbContext db, CancellationToken ct) =>
    {
        var roster = await db.Teams
            .AsNoTracking()
            .OrderBy(t => t.TeamName)
            .Select(t => new TeamRosterDto(
                t.TeamId,
                t.TeamName,
                t.OwnerName,
                t.Roster
                    .OrderBy(r => r.Player.Name)
                    .Select(r => new TeamRosterPlayerDto(
                        r.Player.PlayerGuid,
                        r.PlayerId,
                        r.Player.Name,
                        r.Player.Position.Name,
                        r.Player.Overall,
                        r.Player.Age,
                        db.DraftPicks
                            .Where(p => p.PlayerId == r.PlayerId)
                            .Select(p => p.PickedAtUtc)
                            .FirstOrDefault(),
                        db.DraftPicks
                            .Where(p => p.PlayerId == r.PlayerId)
                            .Select(p => (int?)p.RoundNumber)
                            .FirstOrDefault(),
                        db.DraftPicks
                            .Where(p => p.PlayerId == r.PlayerId)
                            .Select(p => (int?)p.PickInRound)
                            .FirstOrDefault()))
                    .ToList()))
            .ToListAsync(ct);

        return Results.Ok(roster);
    });

    teamsApi.MapGet("/{id:guid}/roster", async (DraftDbContext db, Guid id, CancellationToken ct) =>
    {
        var roster = await db.Teams
            .AsNoTracking()
            .Where(t => t.TeamId == id)
            .Select(t => new TeamRosterDto(
                t.TeamId,
                t.TeamName,
                t.OwnerName,
                t.Roster
                    .OrderBy(r => r.Player.Name)
                    .Select(r => new TeamRosterPlayerDto(
                        r.Player.PlayerGuid,
                        r.PlayerId,
                        r.Player.Name,
                        r.Player.Position.Name,
                        r.Player.Overall,
                        r.Player.Age,
                        db.DraftPicks
                            .Where(p => p.PlayerId == r.PlayerId)
                            .Select(p => p.PickedAtUtc)
                            .FirstOrDefault(),
                        db.DraftPicks
                            .Where(p => p.PlayerId == r.PlayerId)
                            .Select(p => (int?)p.RoundNumber)
                            .FirstOrDefault(),
                        db.DraftPicks
                            .Where(p => p.PlayerId == r.PlayerId)
                            .Select(p => (int?)p.PickInRound)
                            .FirstOrDefault()))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        if (roster is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(roster);
    });

    teamsApi.MapGet("/export/json", async (DraftDbContext db, CancellationToken ct) =>
    {
        var roster = await db.Teams
            .AsNoTracking()
            .OrderBy(t => t.TeamName)
            .Select(t => new
            {
                t.TeamName,
                t.OwnerName,
                Jogadores = t.Roster
                    .OrderBy(r => r.Player.Name)
                    .Select(r => new
                    {
                        r.Player.Name,
                        Posicao = r.Player.Position.Name,
                        r.Player.Overall,
                        r.Player.Age
                    })
                    .ToList()
            })
            .ToListAsync(ct);

        var json = JsonSerializer.Serialize(roster, new JsonSerializerOptions { WriteIndented = true });
        return Results.File(Encoding.UTF8.GetBytes(json), "application/json", "times.json");
    });

    var adminTeamsApi = api.MapGroup("/admin/teams").RequireAuthorization("AdminOnly");

    adminTeamsApi.MapPost(string.Empty, async (ITeamService teamService, TeamCreateDto dto) =>
    {
        try
        {
            var id = await teamService.CreateAsync(dto);
            return Results.Created($"/api/teams/{id}", new { id });
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

    adminTeamsApi.MapPut("/{id:guid}", async (ITeamService teamService, Guid id, TeamUpdateDto dto) =>
    {
        try
        {
            await teamService.UpdateAsync(id, dto);
            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    });

    adminTeamsApi.MapDelete("/{id:guid}", async (ITeamService teamService, Guid id) =>
    {
        try
        {
            await teamService.DeleteAsync(id);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    });

    adminTeamsApi.MapPost("/adjust-budget", async (
        HttpContext httpContext,
        AdminAdjustBudgetRequestDto request,
        AdminTransferService adminTransferService,
        CancellationToken ct) =>
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Payload inválido." });
        }

        if (request.TeamId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "TeamId é obrigatório." });
        }

        if (request.Delta == 0m)
        {
            return Results.BadRequest(new { message = "O ajuste deve ser diferente de zero." });
        }

        if (!TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            await adminTransferService.AdjustBudgetAsync(adminToken!, request.TeamId, request.Delta, request.Reason, ct);
            return Results.Ok(new { message = "Orçamento ajustado com sucesso." });
        }
        catch (AdminForbiddenException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    });
}

static void MapBudgetEndpoints(RouteGroupBuilder api)
{
    var budgetApi = api.MapGroup("/budgets");

    budgetApi.MapGet(
        "/available",
        async ([FromQuery] string? token, DraftDbContext db, IBudgetService budgetService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var teamId = await db.Teams
                .AsNoTracking()
                .Where(t => t.Token == token.Trim())
                .Select(t => (Guid?)t.TeamId)
                .FirstOrDefaultAsync(ct);

            if (!teamId.HasValue)
            {
                return Results.Json(new { message = "Token inválido." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var saldo = await budgetService.GetSaldoAsync(teamId.Value, ct);
            var bloqueado = await budgetService.GetBloqueadoEmLancesAsync(teamId.Value, ct);
            var disponivel = saldo - bloqueado;

            return Results.Ok(new BudgetSummaryDto(teamId.Value, saldo, bloqueado, disponivel));
        })
        .AllowAnonymous();

    budgetApi.MapGet(
        "/{teamId:guid}",
        async (Guid teamId, DraftDbContext db, IBudgetService budgetService, CancellationToken ct) =>
        {
            if (teamId == Guid.Empty)
            {
                return Results.BadRequest(new { message = "TeamId inválido." });
            }

            var teamExists = await db.Teams
                .AsNoTracking()
                .AnyAsync(t => t.TeamId == teamId, ct);

            if (!teamExists)
            {
                return Results.NotFound(new { message = $"Time {teamId} não encontrado." });
            }

            var saldo = await budgetService.GetSaldoAsync(teamId, ct);
            return Results.Ok(new { teamId, saldo });
        })
        .AllowAnonymous();

    var adminBudgetApi = api.MapGroup("/admin/budgets").RequireAuthorization("AdminOnly");

    adminBudgetApi.MapPost(
        "/adjust",
        async (BudgetAdjustRequestDto request, IBudgetService budgetService, CancellationToken ct) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { message = "Payload inválido." });
            }

            if (request.TeamId == Guid.Empty)
            {
                return Results.BadRequest(new { message = "TeamId é obrigatório." });
            }

            if (request.Valor <= 0)
            {
                return Results.BadRequest(new { message = "Valor deve ser maior que zero." });
            }

            if (string.IsNullOrWhiteSpace(request.Origem))
            {
                return Results.BadRequest(new { message = "Origem é obrigatória." });
            }

            var tipo = request.Tipo?.Trim().ToUpperInvariant();
            if (tipo is not ("CREDIT" or "DEBIT"))
            {
                return Results.BadRequest(new { message = "Tipo inválido. Use CREDIT ou DEBIT." });
            }

            try
            {
                await budgetService.RegistrarAjusteAsync(
                    request.TeamId,
                    request.Valor,
                    request.Origem,
                    request.Descricao,
                    tipo == "CREDIT",
                    ct);

                var saldoAtual = await budgetService.GetSaldoAsync(request.TeamId, ct);
                return Results.Ok(new { teamId = request.TeamId, saldo = saldoAtual });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

    adminBudgetApi.MapPost(
        "/apply-match-reward",
        async (MatchRewardRequestDto request, IBudgetService budgetService, CancellationToken ct) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { message = "Payload inválido." });
            }

            try
            {
                var result = await budgetService.ApplyMatchRewardAsync(request, ct);

                if (!result.AjusteRealizado)
                {
                    return Results.Ok(new
                    {
                        message = "Sem alteração.",
                        teamId = result.TeamId,
                        saldo = result.SaldoAtual
                    });
                }

                return Results.Ok(new
                {
                    teamId = result.TeamId,
                    valorAplicado = result.ValorAplicado,
                    saldo = result.SaldoAtual,
                    tipo = result.Tipo,
                    descricao = result.Descricao
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

    adminBudgetApi.MapGet("/ledger", async ([FromQuery] Guid teamId, DraftDbContext db, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20 ) =>
        {
            if (teamId == Guid.Empty)
            {
                return Results.BadRequest(new { message = "teamId é obrigatório." });
            }

            if (page < 1 || pageSize < 1)
            {
                return Results.BadRequest(new { message = "Parâmetros de paginação inválidos." });
            }

            var size = Math.Min(pageSize, 100);

            var teamExists = await db.Teams
                .AsNoTracking()
                .AnyAsync(t => t.TeamId == teamId, ct);

            if (!teamExists)
            {
                return Results.NotFound(new { message = $"Time {teamId} não encontrado." });
            }

            var query = db.BudgetLedgers
                .AsNoTracking()
                .Where(l => l.TeamId == teamId);

            var total = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(l => l.DataUtc)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(l => new LedgerItemDto(l.DataUtc, l.Tipo, l.Origem, l.Valor, l.Descricao))
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<LedgerItemDto>(items, total));
        });
}
static void MapPricingEndpoints(RouteGroupBuilder api)
{
    var pricingApi = api.MapGroup("/pricing");

    pricingApi.MapGet(
        "/preview",
        async (
            [FromQuery(Name = "pos")] string? positionCode,
            [FromQuery(Name = "posId")] short? positionId,
            [FromQuery] int? age,
            [FromQuery(Name = "ovr")] int? overall,
            IPricingService pricingService,
            CancellationToken ct) =>
        {
            if (!age.HasValue)
            {
                return Results.BadRequest(new { message = "Parâmetro 'age' é obrigatório." });
            }

            if (!overall.HasValue)
            {
                return Results.BadRequest(new { message = "Parâmetro 'ovr' é obrigatório." });
            }

            if (string.IsNullOrWhiteSpace(positionCode) && !positionId.HasValue)
            {
                return Results.BadRequest(new { message = "Informe 'pos' ou 'posId'." });
            }

            try
            {
                var result = await pricingService.CalculateForPositionAsync(positionCode, positionId, age.Value, overall.Value, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

    pricingApi.MapGet(
        "/preview/{playerId:int}",
        async (int playerId, IPricingService pricingService, CancellationToken ct) =>
        {
            try
            {
                var result = await pricingService.CalculateForPlayerAsync(playerId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = $"Jogador {playerId} não encontrado." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });
}

static void MapTransfersEndpoints(RouteGroupBuilder api)
{
    var transfersApi = api.MapGroup("/transfers");

    transfersApi.MapGet("/history", async (
        [FromQuery] Guid? teamId,
        [FromQuery] Guid? playerId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ITransfersQueryService transfersQueryService,
        CancellationToken ct) =>
    {
        TransferType? typeFilter = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim();
            if (Enum.TryParse<TransferType>(normalizedType, ignoreCase: true, out var parsedType))
            {
                typeFilter = parsedType;
            }
            else if (int.TryParse(normalizedType, out var numericType) && Enum.IsDefined(typeof(TransferType), numericType))
            {
                typeFilter = (TransferType)numericType;
            }
            else
            {
                return Results.BadRequest(new { message = "Tipo de transferência inválido." });
            }
        }

        var filter = new TransfersFilter
        {
            TeamId = teamId,
            PlayerId = playerId,
            Type = typeFilter,
            FromUtc = from,
            ToUtc = to,
            Page = page ?? 1,
            PageSize = pageSize ?? 20
        };

        try
        {
            var result = await transfersQueryService.QueryHistoryAsync(filter, ct);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }).AllowAnonymous();
}

static void MapMarketEndpoints(RouteGroupBuilder api)
{
    var marketApi = api.MapGroup("/market");

    MapMarketItemPublicationEndpoints(marketApi);

    marketApi.MapGet(
        "/history",
        async ([FromQuery] Guid? teamId, [FromQuery] int? take, ITransferHistoryService transferHistoryService) =>
        {
            var size = take ?? 50;

            try
            {
                var historyItems = teamId.HasValue
                    ? await transferHistoryService.GetTransfersByTeamAsync(teamId.Value, size)
                    : await transferHistoryService.GetRecentTransfersAsync(size);

                var response = historyItems
                    .Select(MapTransferHistoryToDto)
                    .ToList();

                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .AllowAnonymous();

    marketApi.MapPost(
        "/history",
        async (RegisterTransferHistoryRequestDto request, ITransferHistoryService transferHistoryService) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { message = "Payload inválido." });
            }

            var entry = new TransferHistory
            {
                TransferId = request.TransferId ?? Guid.Empty,
                PlayerId = request.PlayerId,
                FromTeamId = request.FromTeamId,
                ToTeamId = request.ToTeamId,
                Amount = request.Amount,
                Type = request.Type,
                Notes = request.Notes,
                PerformedBy = request.PerformedBy,
                PerformedAtUtc = request.PerformedAtUtc ?? default
            };

            try
            {
                await transferHistoryService.RegisterTransferAsync(entry);

                var saved = (await transferHistoryService.GetRecentTransfersAsync(1)).FirstOrDefault(h => h.TransferId == entry.TransferId);
                var result = saved is not null ? MapTransferHistoryToDto(saved) : MapTransferHistoryToDto(entry);

                return Results.Created($"/api/market/history/{entry.TransferId}", result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .RequireAuthorization("AdminOnly");

    marketApi.MapGet(string.Empty, async (IMarketService marketService, CancellationToken ct) =>
    {
        await marketService.EnsureCycleAsync(ct);
        await marketService.CloseExpiredItemsAsync(ct);
        var items = await marketService.GetActiveItemsAsync(ct);
        return Results.Ok(items);
    }).AllowAnonymous();

    marketApi.MapGet("/{itemId:guid}", async (Guid itemId, IMarketService marketService, CancellationToken ct) =>
    {
        await marketService.CloseExpiredItemsAsync(ct);
        var item = await marketService.GetItemAsync(itemId, ct);
        return item is null
            ? Results.NotFound(new { message = "Item não encontrado." })
            : Results.Ok(item);
    }).AllowAnonymous();

    marketApi.MapPost("/{itemId:guid}/bid", async (
        Guid itemId,
        MarketBidRequest request,
        HttpContext context,
        IMarketService marketService,
        CancellationToken ct) =>
    {
        var token = GetTeamToken(context, request.TeamToken);
        if (token is null)
        {
            return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        try
        {
            var result = await marketService.PlaceBidAsync(itemId, token, request.Amount, ct);
            return Results.Ok(result);
        }
        catch (MarketForbiddenException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (MarketValidationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (MarketConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (MarketNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }).AllowAnonymous();

    marketApi.MapPost("/{itemId:guid}/buy-now", async (
        Guid itemId,
        MarketBuyNowRequest request,
        HttpContext context,
        IMarketService marketService,
        CancellationToken ct) =>
    {
        var token = GetTeamToken(context, request.TeamToken);
        if (token is null)
        {
            return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        try
        {
            var result = await marketService.BuyNowAsync(itemId, token, ct);
            return Results.Ok(result);
        }
        catch (MarketForbiddenException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (MarketValidationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (MarketConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (MarketNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }).AllowAnonymous();

    var adminMarketApi = api.MapGroup("/admin/market").RequireAuthorization("AdminOnly");

    adminMarketApi.MapPost("/refresh", async (IMarketCycleGenerator cycleGenerator, CancellationToken ct) =>
    {
        var now = DateTime.UtcNow;
        var needsNew = await cycleGenerator.NeedsNewCycleAsync(now, ct);
        if (!needsNew)
        {
            return Results.Conflict(new { message = "Já existe um ciclo ativo." });
        }

        var cycle = await cycleGenerator.CreateNewCycleAsync(now, ct);
        return Results.Ok(cycle);
    });

    adminMarketApi.MapPost("/close-expired", async (IMarketService marketService, CancellationToken ct) =>
    {
        var closed = await marketService.CloseExpiredItemsAsync(ct);
        return Results.Ok(new { itensFechados = closed });
    });

    adminMarketApi.MapPost("/cancel/{itemId:guid}", async (
        Guid itemId,
        AdminCancelMarketItemRequestDto request,
        HttpContext httpContext,
        AdminTransferService adminTransferService,
        CancellationToken ct) =>
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Payload inválido." });
        }

        if (!TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            await adminTransferService.CancelMarketItemAsync(adminToken!, itemId, request.Reason, ct);
            return Results.Ok(new { message = "Item cancelado com sucesso." });
        }
        catch (AdminForbiddenException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (AdminConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    });

    static string? GetTeamToken(HttpContext context, string? payloadToken)
    {
        var headerToken = context.Request.Headers["X-Team-Token"].FirstOrDefault();
        var token = !string.IsNullOrWhiteSpace(payloadToken) ? payloadToken : headerToken;
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }
}

static void MapMarketItemPublicationEndpoints(RouteGroupBuilder marketApi)
{
    var itemsApi = marketApi.MapGroup("/items")
        .RequireAuthorization("AdminOnly");

    itemsApi.MapGet(string.Empty, async (IMarketItemPublicationService service, CancellationToken ct) =>
    {
        var items = await service.ListAsync(ct).ConfigureAwait(false);
        return Results.Ok(items);
    });

    itemsApi.MapGet("/{itemId:guid}", async (
        Guid itemId,
        HttpContext context,
        IMarketItemPublicationService service,
        CancellationToken ct) =>
    {
        var item = await service.GetAsync(itemId, ct).ConfigureAwait(false);
        if (item is null)
        {
            var problem = new ProblemDetails
            {
                Title = "Item não encontrado.",
                Detail = "O item solicitado não existe ou foi removido.",
                Status = StatusCodes.Status404NotFound,
                Type = "https://httpstatuses.com/404"
            };

            return Results.Problem(problem);
        }

        ApplyEtag(context.Response, item.RowVersion);
        return Results.Ok(item);
    });

    itemsApi.MapPost(string.Empty, async (
        MarketItemDraftCreateRequest request,
        HttpContext context,
        IMarketItemPublicationService service,
        CancellationToken ct) =>
    {
        try
        {
            var created = await service.CreateDraftAsync(request, ct).ConfigureAwait(false);
            ApplyEtag(context.Response, created.RowVersion);
            return Results.Created($"/api/market/items/{created.ItemId}", created);
        }
        catch (MarketItemValidationException ex)
        {
            return CreateValidationProblem(ex);
        }
        catch (MarketConflictException ex)
        {
            return CreateConflictProblem(ex.Message);
        }
    });

    itemsApi.MapPut("/{itemId:guid}", async (
        Guid itemId,
        MarketItemDraftUpdateRequest request,
        HttpContext context,
        IMarketItemPublicationService service,
        CancellationToken ct) =>
    {
        if (!TryResolveRowVersion(context.Request, out var rowVersion, out var error))
        {
            return error!;
        }

        try
        {
            var updated = await service.UpdateDraftAsync(itemId, request, rowVersion, ct).ConfigureAwait(false);
            ApplyEtag(context.Response, updated.RowVersion);
            return Results.Ok(updated);
        }
        catch (MarketItemValidationException ex)
        {
            return CreateValidationProblem(ex);
        }
        catch (MarketNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
        }
        catch (MarketConflictException ex)
        {
            return CreateConflictProblem(ex.Message);
        }
        catch (MarketPreconditionFailedException ex)
        {
            return CreatePreconditionFailedProblem(ex.Message);
        }
    });

    itemsApi.MapPost("/{itemId:guid}/publish", async (
        Guid itemId,
        HttpContext context,
        IMarketItemPublicationService service,
        CancellationToken ct) =>
    {
        if (!TryResolveRowVersion(context.Request, out var rowVersion, out var error))
        {
            return error!;
        }

        try
        {
            var published = await service.PublishAsync(itemId, rowVersion, ct).ConfigureAwait(false);
            ApplyEtag(context.Response, published.RowVersion);
            return Results.Ok(published);
        }
        catch (MarketNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
        }
        catch (MarketConflictException ex)
        {
            return CreateConflictProblem(ex.Message);
        }
        catch (MarketPreconditionFailedException ex)
        {
            return CreatePreconditionFailedProblem(ex.Message);
        }
    });

    itemsApi.MapDelete("/{itemId:guid}", async (
        Guid itemId,
        HttpContext context,
        IMarketItemPublicationService service,
        CancellationToken ct) =>
    {
        if (!TryResolveRowVersion(context.Request, out var rowVersion, out var error))
        {
            return error!;
        }

        try
        {
            var deleted = await service.SoftDeleteAsync(itemId, rowVersion, ct).ConfigureAwait(false);
            ApplyEtag(context.Response, deleted.RowVersion);
            return Results.NoContent();
        }
        catch (MarketNotFoundException ex)
        {
            return CreateNotFoundProblem(ex.Message);
        }
        catch (MarketConflictException ex)
        {
            return CreateConflictProblem(ex.Message);
        }
        catch (MarketPreconditionFailedException ex)
        {
            return CreatePreconditionFailedProblem(ex.Message);
        }
    });
}

static void ApplyEtag(HttpResponse response, uint rowVersion)
{
    response.Headers[HeaderNames.ETag] = $"W/\"{rowVersion}\"";
}

static bool TryResolveRowVersion(HttpRequest request, out uint rowVersion, out IResult? errorResult)
{
    rowVersion = 0;
    errorResult = null;

    if (!request.Headers.TryGetValue(HeaderNames.IfMatch, out var headerValues))
    {
        errorResult = TypedResults.Problem(
            title: "Pré-condição obrigatória.",
            detail: "O cabeçalho If-Match é obrigatório para esta operação.",
            statusCode: StatusCodes.Status428PreconditionRequired,
            type: "https://httpstatuses.com/428"
        );
        return false;
    }

    var rawValue = headerValues.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        errorResult = TypedResults.Problem(
            title: "Cabeçalho If-Match inválido.",
            detail: "O valor informado no cabeçalho If-Match é inválido.",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400"
        );
        return false;
    }

    var parsed = rawValue.Trim();
    if (parsed.StartsWith("W/\"", StringComparison.OrdinalIgnoreCase) && parsed.EndsWith("\""))
    {
        parsed = parsed[3..^1];
    }
    else if (parsed.StartsWith('"') && parsed.EndsWith('"'))
    {
        parsed = parsed[1..^1];
    }

    if (!uint.TryParse(parsed, out rowVersion))
    {
        errorResult = TypedResults.Problem(
            title: "Cabeçalho If-Match inválido.",
            detail: "Não foi possível interpretar o valor do cabeçalho If-Match.",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://httpstatuses.com/400"
        );
        return false;
    }

    return true;
}

static IResult CreateValidationProblem(MarketItemValidationException ex)
{
    var problem = new ProblemDetails
    {
        Title = "Falha na validação do item de mercado.",
        Detail = ex.Message,
        Status = StatusCodes.Status422UnprocessableEntity,
        Type = "https://httpstatuses.com/422"
    };
    problem.Extensions["errors"] = ex.Errors; 

    return Results.Problem(problem);
}

static IResult CreateConflictProblem(string message)
{
    var problem = new ProblemDetails
    {
        Title = "Conflito ao processar o item.",
        Detail = message,
        Status = StatusCodes.Status409Conflict,
        Type = "https://httpstatuses.com/409"
    };

    return Results.Problem(problem); // <-- use Results aqui
}

static IResult CreateNotFoundProblem(string message)
{
    var problem = new ProblemDetails
    {
        Title = "Item não encontrado.",
        Detail = message,
        Status = StatusCodes.Status404NotFound,
        Type = "https://httpstatuses.com/404"
    };

    return Results.Problem(problem);
}

static IResult CreatePreconditionFailedProblem(string message)
{
    var problem = new ProblemDetails
    {
        Title = "Pré-condição não satisfeita.",
        Detail = message,
        Status = StatusCodes.Status412PreconditionFailed,
        Type = "https://httpstatuses.com/412"
    };

    return Results.Problem(problem);
}
static TransferHistoryItemDto MapTransferHistoryToDto(TransferHistory history)
{
    if (history is null)
    {
        throw new ArgumentNullException(nameof(history));
    }

    var playerName = history.Player?.Name ?? string.Empty;
    var fromTeamName = history.FromTeam?.TeamName ?? "Mercado Livre";
    var toTeamName = history.ToTeam?.TeamName;

    return new TransferHistoryItemDto(
        history.TransferId,
        history.PerformedAtUtc,
        history.PlayerId,
        playerName,
        history.FromTeamId,
        fromTeamName,
        history.ToTeamId,
        toTeamName,
        history.Amount,
        (int)history.Type,
        history.Type.ToDisplayName(),
        history.Notes,
        history.PerformedBy);
}

static bool TryGetAdminToken(HttpContext context, out string? adminToken, out IResult? errorResult)
{
    adminToken = null;
    errorResult = null;

    if (!context.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
    {
        errorResult = Results.Json(new { message = "Token de administrador ausente." }, statusCode: StatusCodes.Status403Forbidden);
        return false;
    }

    var headerValue = authorizationHeader.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(headerValue))
    {
        errorResult = Results.Json(new { message = "Token de administrador ausente." }, statusCode: StatusCodes.Status403Forbidden);
        return false;
    }

    const string bearerPrefix = "Bearer ";
    if (!headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
        errorResult = Results.Json(new { message = "Token de administrador inválido." }, statusCode: StatusCodes.Status403Forbidden);
        return false;
    }

    var tokenValue = headerValue[bearerPrefix.Length..].Trim();
    if (string.IsNullOrWhiteSpace(tokenValue))
    {
        errorResult = Results.Json(new { message = "Token de administrador inválido." }, statusCode: StatusCodes.Status403Forbidden);
        return false;
    }

    adminToken = tokenValue;
    return true;
}

static async Task<List<PlayerExportDto>> LoadPlayerExportAsync(DraftDbContext db, CancellationToken ct)
{
    var players = await db.Players
        .AsNoTracking()
        .OrderBy(p => p.Name)
        .Select(p => new
        {
            p.Name,
            Posicao = p.Position.Name,
            p.Overall,
            TemTime = p.TeamRosters.Any(),
            Time = p.TeamRosters
                .OrderBy(r => r.TeamId)
                .Select(r => r.Team.TeamName)
                .FirstOrDefault()
        })
        .ToListAsync(ct);

    return players
        .Select(p => new PlayerExportDto(
            p.Name,
            p.Posicao,
            p.Overall,
            p.TemTime ? "Escolhido" : "Disponível",
            p.Time))
        .ToList();
}

static string BuildPlayerCsv(IReadOnlyList<PlayerExportDto> players)
{
    var sb = new StringBuilder();
    sb.AppendLine("Nome;Posição;Overall;Status;Time");
    foreach (var player in players)
    {
        sb.AppendLine($"{Escape(player.Nome)};{Escape(player.Posicao)};{player.Overall};{Escape(player.Status)};{Escape(player.Time)}");
    }

    return sb.ToString();
}

static string BuildDraftBoardCsv(IReadOnlyList<DraftBoardExportDto> entries)
{
    var sb = new StringBuilder();
    sb.AppendLine("Rodada;Escolha;Time;Responsável;Jogador;Posição;Data/Hora");
    foreach (var entry in entries)
    {
        sb.AppendLine($"{entry.Rodada};{entry.Escolha};{Escape(entry.Time)};{Escape(entry.Responsavel)};{Escape(entry.Jogador)};{Escape(entry.Posicao)};{Escape(entry.DataHoraUtc)}");
    }

    return sb.ToString();
}

static string Escape(string? value)
{
    if (string.IsNullOrEmpty(value))
    {
        return string.Empty;
    }

    var sanitized = value.Replace("\"", "''");
    return sanitized.Contains(';') ? $"\"{sanitized}\"" : sanitized;
}

record MarketBidRequest(decimal Amount, string? TeamToken);

record MarketBuyNowRequest(string? TeamToken);
