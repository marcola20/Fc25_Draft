using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Repositories;
using Fc25Draft.Infra.Services;
using Fc25Draft.Web.Extensions;
using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Security;
using Fc25Draft.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

//var connectionString =
//    builder.Configuration.GetConnectionString("DefaultConnection")
//    ?? Environment.GetEnvironmentVariable("SQLCONNSTR_DefaultConnection")
//    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

//builder.Services.AddDbContext<DraftDbContext>(opt =>
//    opt.UseSqlServer(connectionString, sql =>
//    {
//        sql.MigrationsAssembly(typeof(DraftDbContext).Assembly.FullName);
//        sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null); 
//    })
//       .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
//       .EnableDetailedErrors(builder.Environment.IsDevelopment())
//);

builder.Services.AddDbContext<DraftDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


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
builder.Services.AddScoped<AdminTransferApiClient>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IMarketCycleGenerator, MarketCycleGenerator>();
builder.Services.AddScoped<IMarketService, MarketService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IAdminTransferService, AdminTransferService>();
builder.Services.AddScoped<ITransfersQueryService, TransfersQueryService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
    await db.Database.MigrateAsync();
}

await app.SeedDatabaseAsync();

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

var api = app.MapGroup("/api");

MapAdminEndpoints(api);
MapDraftEndpoints(api);
MapPlayerEndpoints(api);
MapTeamEndpoints(api);
MapPricingEndpoints(api);
MapMarketEndpoints(api);
MapBudgetEndpoints(api);
MapAdminTransferEndpoints(api);
MapTransferHistoryEndpoints(api);

app.Run();

static void MapAdminEndpoints(RouteGroupBuilder api)
{
    var adminApi = api.MapGroup("/admin").RequireAuthorization("AdminOnly");

    adminApi.MapGet("/validate", () => Results.Ok(new { status = "ok" }));
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
        short? pos,
        bool? onlyAvailable,
        int? overallMin,
        int? overallMax,
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
            query = query.Where(p => EF.Functions.Like(p.Name, pattern));
        }

        if (pos.HasValue)
        {
            query = query.Where(p => p.PositionId == pos.Value);
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

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.Overall)
            .ThenBy(p => p.Name)
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
                        r.Player.PublicId,
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
                        r.Player.PublicId,
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

static void MapAdminTransferEndpoints(RouteGroupBuilder api)
{
    var adminTransferApi = api.MapGroup("/admin/transfer").RequireAuthorization("AdminOnly");

    adminTransferApi.MapPost(
        "/sell",
        async (HttpContext httpContext, SellRequest request, IAdminTransferService transferService, CancellationToken ct) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { message = "Payload inválido." });
            }

            try
            {
                var token = ExtractAdminToken(httpContext);
                var result = await transferService.SellAsync(token, request.FromTeamId, request.ToTeamId, request.PlayerIds?.ToArray() ?? Array.Empty<Guid>(), request.Amount, request.Reason ?? string.Empty, ct);
                return Results.Ok(result);
            }
            catch (AdminForbiddenException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (AdminValidationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (AdminConflictException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { message = "Os dados foram atualizados por outra operação. Recarregue as informações e tente novamente." }, statusCode: StatusCodes.Status409Conflict);
            }
        });

    adminTransferApi.MapPost(
        "/swap",
        async (HttpContext httpContext, SwapRequest request, IAdminTransferService transferService, CancellationToken ct) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { message = "Payload inválido." });
            }

            try
            {
                var token = ExtractAdminToken(httpContext);
                var result = await transferService.SwapAsync(
                    token,
                    request.TeamAId,
                    request.PlayersFromA?.ToArray() ?? Array.Empty<Guid>(),
                    request.TeamBId,
                    request.PlayersFromB?.ToArray() ?? Array.Empty<Guid>(),
                    request.CashAdjustFromAToB,
                    request.Reason ?? string.Empty,
                    ct);

                return Results.Ok(result);
            }
            catch (AdminForbiddenException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (AdminValidationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (AdminConflictException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { message = "Os dados foram atualizados por outra operação. Recarregue as informações e tente novamente." }, statusCode: StatusCodes.Status409Conflict);
            }
        });

    adminTransferApi.MapPost(
        "/move",
        async (HttpContext httpContext, MoveRequest request, IAdminTransferService transferService, CancellationToken ct) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { message = "Payload inválido." });
            }

            try
            {
                var token = ExtractAdminToken(httpContext);
                var result = await transferService.MoveAsync(token, request.PlayerId, request.ToTeamId, request.Reason ?? string.Empty, ct);
                return Results.Ok(result);
            }
            catch (AdminForbiddenException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (AdminValidationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (AdminConflictException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { message = "Os dados foram atualizados por outra operação. Recarregue as informações e tente novamente." }, statusCode: StatusCodes.Status409Conflict);
            }
        });

    var adminTeamsApi = api.MapGroup("/admin/teams").RequireAuthorization("AdminOnly");

    adminTeamsApi.MapPost(
        "/adjust-budget",
        async (HttpContext httpContext, AdjustBudgetRequest request, IAdminTransferService transferService, CancellationToken ct) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { message = "Payload inválido." });
            }

            try
            {
                var token = ExtractAdminToken(httpContext);
                var result = await transferService.AdjustBudgetAsync(token, request.TeamId, request.Delta, request.Reason ?? string.Empty, ct);
                return Results.Ok(result);
            }
            catch (AdminForbiddenException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (AdminValidationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { message = "Os dados foram atualizados por outra operação. Recarregue as informações e tente novamente." }, statusCode: StatusCodes.Status409Conflict);
            }
        });

    var adminMarketApi = api.MapGroup("/admin/market").RequireAuthorization("AdminOnly");

    adminMarketApi.MapPost(
        "/cancel/{itemId:guid}",
        async (HttpContext httpContext, Guid itemId, CancelMarketItemRequest request, IAdminTransferService transferService, CancellationToken ct) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { message = "Payload inválido." });
            }

            try
            {
                var token = ExtractAdminToken(httpContext);
                var result = await transferService.CancelMarketItemAsync(token, itemId, request.Reason ?? string.Empty, ct);
                return Results.Ok(result);
            }
            catch (AdminForbiddenException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (AdminValidationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (AdminConflictException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Json(new { message = "Os dados foram atualizados por outra operação. Recarregue as informações e tente novamente." }, statusCode: StatusCodes.Status409Conflict);
            }
        });
}

static void MapTransferHistoryEndpoints(RouteGroupBuilder api)
{
    var transferApi = api.MapGroup("/transfers");

    transferApi.MapGet(
        "/history",
        async (
            [FromQuery] Guid? teamId,
            [FromQuery] Guid? playerId,
            [FromQuery] int? type,
            [FromQuery(Name = "from")] DateTime? fromUtc,
            [FromQuery(Name = "to")] DateTime? toUtc,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            ITransfersQueryService queryService,
            CancellationToken ct) =>
        {
            var currentPage = page > 0 ? page : 1;
            var currentPageSize = pageSize > 0 ? pageSize : 50;

            try
            {
                var filter = new TransfersFilter(teamId, playerId, type, fromUtc, toUtc, currentPage, currentPageSize);
                var result = await queryService.QueryHistoryAsync(filter, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });
}

static string ExtractAdminToken(HttpContext httpContext)
{
    if (!httpContext.Request.Headers.TryGetValue("Authorization", out var values))
    {
        return string.Empty;
    }

    var headerValue = values.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(headerValue))
    {
        return string.Empty;
    }

    const string prefix = "Bearer ";
    if (headerValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return headerValue[prefix.Length..].Trim();
    }

    return headerValue.Trim();
}

internal sealed record SellRequest(Guid FromTeamId, Guid ToTeamId, IReadOnlyCollection<Guid>? PlayerIds, decimal Amount, string? Reason);
internal sealed record SwapRequest(Guid TeamAId, IReadOnlyCollection<Guid>? PlayersFromA, Guid TeamBId, IReadOnlyCollection<Guid>? PlayersFromB, decimal CashAdjustFromAToB, string? Reason);
internal sealed record MoveRequest(Guid PlayerId, Guid ToTeamId, string? Reason);
internal sealed record AdjustBudgetRequest(Guid TeamId, decimal Delta, string? Reason);
internal sealed record CancelMarketItemRequest(string? Reason);
internal sealed record TransferHistoryItemDto(DateTime PerformedAtUtc, Guid PlayerId, string PlayerName, string Tipo, string De, string? Para, decimal Valor);
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

static void MapMarketEndpoints(RouteGroupBuilder api)
{
    var marketApi = api.MapGroup("/market");

    marketApi.MapGet(
        "/history",
        async ([FromQuery] int page, [FromQuery] int pageSize, DraftDbContext db, CancellationToken ct) =>
        {
            var pageNumber = page < 1 ? 1 : page;
            var size = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

            var query = db.TransferHistories
                .AsNoTracking();

            var total = await query.CountAsync(ct);

            var historyRows = await query
                .OrderByDescending(h => h.PerformedAtUtc)
                .Skip((pageNumber - 1) * size)
                .Take(size)
                .Select(h => new
                {
                    h.PerformedAtUtc,
                    h.PlayerPublicId,
                    PlayerName = h.Player.Name,
                    h.Type,
                    FromTeamName = h.FromTeam != null ? h.FromTeam.TeamName : null,
                    ToTeamName = h.ToTeam != null ? h.ToTeam.TeamName : null,
                    h.Amount
                })
                .ToListAsync(ct);

            var items = historyRows
                .Select(h => new TransferHistoryItemDto(
                    h.PerformedAtUtc,
                    h.PlayerPublicId,
                    h.PlayerName,
                    TranslateTransferType(h.Type),
                    h.FromTeamName ?? "Mercado Livre",
                    h.ToTeamName,
                    h.Amount ?? 0m))
                .ToList();

            return Results.Ok(new PagedResult<TransferHistoryItemDto>(items, total));
        })
        .AllowAnonymous();

    marketApi.MapGet(
        "/history/by-team/{teamId:guid}",
        async (Guid teamId, [FromQuery] int page, [FromQuery] int pageSize, DraftDbContext db, CancellationToken ct) =>
        {
            if (teamId == Guid.Empty)
            {
                return Results.BadRequest(new { message = "teamId é obrigatório." });
            }

            var teamExists = await db.Teams
                .AsNoTracking()
                .AnyAsync(t => t.TeamId == teamId, ct);

            if (!teamExists)
            {
                return Results.NotFound(new { message = $"Time {teamId} não encontrado." });
            }

            var pageNumber = page < 1 ? 1 : page;
            var size = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

            var query = db.TransferHistories
                .AsNoTracking()
                .Where(h => h.FromTeamId == teamId || h.ToTeamId == teamId);

            var total = await query.CountAsync(ct);

            var historyRows = await query
                .OrderByDescending(h => h.PerformedAtUtc)
                .Skip((pageNumber - 1) * size)
                .Take(size)
                .Select(h => new
                {
                    h.PerformedAtUtc,
                    h.PlayerPublicId,
                    PlayerName = h.Player.Name,
                    h.Type,
                    FromTeamName = h.FromTeam != null ? h.FromTeam.TeamName : null,
                    ToTeamName = h.ToTeam != null ? h.ToTeam.TeamName : null,
                    h.Amount
                })
                .ToListAsync(ct);

            var items = historyRows
                .Select(h => new TransferHistoryItemDto(
                    h.PerformedAtUtc,
                    h.PlayerPublicId,
                    h.PlayerName,
                    TranslateTransferType(h.Type),
                    h.FromTeamName ?? "Mercado Livre",
                    h.ToTeamName,
                    h.Amount ?? 0m))
                .ToList();

            return Results.Ok(new PagedResult<TransferHistoryItemDto>(items, total));
        })
        .AllowAnonymous();

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

    static string? GetTeamToken(HttpContext context, string? payloadToken)
    {
        var headerToken = context.Request.Headers["X-Team-Token"].FirstOrDefault();
        var token = !string.IsNullOrWhiteSpace(payloadToken) ? payloadToken : headerToken;
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    static string TranslateTransferType(TransferType type)
        => type switch
        {
            TransferType.Auction => "Leilão",
            TransferType.BuyNow => "Compra imediata",
            TransferType.Sale => "Venda",
            TransferType.Swap => "Troca",
            TransferType.AdminMove => "Movimentação administrativa",
            _ => type.ToString()
        };
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
