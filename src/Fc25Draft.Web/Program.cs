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
builder.Services.Configure<MarketGenerationOptions>(builder.Configuration.GetSection(MarketGenerationOptions.SectionName));

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
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IMarketService, MarketService>();

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
                t.TeamToken,
                Jogadores = t.Roster.Count
            })
            .FirstOrDefaultAsync(ct);

        if (team is null)
        {
            return Results.NotFound();
        }

        var includeToken = httpContext.User.IsInRole("Admin");
        var teamToken = includeToken ? team.TeamToken : Guid.Empty;

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
                var result = await pricingService.CalculateAsync(positionCode, positionId, age.Value, overall.Value, ct);
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

    marketApi.MapGet(string.Empty, async (IMarketService marketService, CancellationToken ct) =>
    {
        var items = await marketService.GetOpenItemsAsync(ct);
        return Results.Ok(items);
    }).AllowAnonymous();

    marketApi.MapGet("/{marketItemId:guid}", async (Guid marketItemId, IMarketService marketService, CancellationToken ct) =>
    {
        var items = await marketService.GetOpenItemsAsync(ct);
        var item = items.FirstOrDefault(i => i.MarketItemId == marketItemId);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }).AllowAnonymous();

    var adminMarketApi = api.MapGroup("/admin/market").RequireAuthorization("AdminOnly");

    adminMarketApi.MapPost("/generate", async (IMarketService marketService, CancellationToken ct) =>
    {
        try
        {
            var items = await marketService.GenerateRoundAsync(ct);
            var dtos = items.Select(ToDto).ToList();
            return Results.Created("/api/market", dtos);
        }
        catch (MarketGenerationConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (MarketGenerationValidationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    });

    static TransferMarketItemDto ToDto(TransferMarketItem item)
    {
        if (item.Player is null)
        {
            throw new InvalidOperationException("Jogador não carregado para o item de mercado.");
        }

        var positionName = item.Player.Position?.Name ?? string.Empty;
        var age = item.Player.Age ?? 0;

        return new TransferMarketItemDto(
            item.MarketItemId,
            item.PlayerId,
            item.Player.Name,
            positionName,
            age,
            item.Player.Overall,
            item.PrecoBase,
            item.PrecoComprarAgora,
            item.LanceAtual,
            item.MaiorLanceTeam?.TeamName ?? string.Empty,
            item.Status,
            item.DataInicioUtc);
    }
}

static async Task<List<PlayerExportDto>> LoadPlayerExportAsync(DraftDbContext db, CancellationToken ct)
{
    return await db.Players
        .AsNoTracking()
        .OrderBy(p => p.Name)
        .Select(p => new PlayerExportDto(
            p.Name,
            p.Position.Name,
            p.Overall,
            p.TeamRosters.Any() ? "Escolhido" : "Disponível",
            p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault()))
        .ToListAsync(ct);
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
        sb.AppendLine($"{entry.Rodada};{entry.Escolha};{Escape(entry.Time)};{Escape(entry.Responsavel)};{Escape(entry.Jogador)};{Escape(entry.Posicao)};{{entry.DataHoraUtc.ToLocalTime().ToString(\"dd/MM/yyyy HH:mm:ss\", new System.Globalization.CultureInfo(\"pt-BR\"))}}\r\n");
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
