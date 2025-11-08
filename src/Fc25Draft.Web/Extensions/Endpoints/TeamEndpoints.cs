using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class TeamEndpoints
    {
        public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder api)
        {
            var teamsApi = api.MapGroup("/teams");

            teamsApi.MapGet(string.Empty, async (
                DraftDbContext db,
                string? q,
                int page = 1,
                int pageSize = 10,
                CancellationToken ct = default) =>
            {
                var currentPage = page < 1 ? 1 : page;
                var currentPageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 100);

                var query = db.Teams.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var pattern = $"%{q.Trim()}%";
                    query = query.Where(t =>
                        EF.Functions.Like(t.TeamName, pattern) ||
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

                return Results.Ok(new PagedResult<TeamListItemDto>(items, total, currentPage, currentPageSize));
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
                        Jogadores = t.Roster.Count,
                        t.Budget
                    })
                    .FirstOrDefaultAsync(ct);

                if (team is null) return Results.NotFound();

                var includeToken = httpContext.User.IsInRole("Admin");
                var teamToken = includeToken ? team.Token : string.Empty;
                var budgetFormatado = string.Format(new System.Globalization.CultureInfo("pt-BR"), "{0:C}", team.Budget);

                var dto = new TeamDetailsDto(team.TeamId, team.TeamName, team.OwnerName, teamToken, team.Jogadores, budgetFormatado);
                return Results.Ok(dto);
            });

            teamsApi.MapGet("/me", async (DraftDbContext db, HttpContext httpContext, CancellationToken ct) =>
            {
                var token = httpContext.Request.Headers["X-Team-Token"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);
                }

                var normalized = token.Trim();

                var identity = await db.Teams
                    .AsNoTracking()
                    .Where(t => t.Token == normalized)
                    .Select(t => new TeamIdentityDto(t.TeamId, t.TeamName))
                    .FirstOrDefaultAsync(ct);

                if (identity is null)
                {
                    return Results.Json(new { message = "Token inválido." }, statusCode: StatusCodes.Status403Forbidden);
                }

                return Results.Ok(identity);
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
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => p.PickedAtUtc).FirstOrDefault(),
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => (int?)p.RoundNumber).FirstOrDefault(),
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => (int?)p.PickInRound).FirstOrDefault()))
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
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => p.PickedAtUtc).FirstOrDefault(),
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => (int?)p.RoundNumber).FirstOrDefault(),
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => (int?)p.PickInRound).FirstOrDefault()))
                            .ToList()))
                    .FirstOrDefaultAsync(ct);

                return roster is null ? Results.NotFound() : Results.Ok(roster);
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

            teamsApi.MapPost("/{teamId:guid}/quick-sell/{playerId:guid}", async (
                Guid teamId,
                Guid playerId,
                HttpContext context,
                DraftDbContext db,
                IPricingService pricingService,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("QuickSellEndpoint");
                var token = context.Request.Headers["X-Team-Token"].FirstOrDefault();

                if (string.IsNullOrWhiteSpace(token))
                {
                    return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);
                }

                var normalizedToken = token.Trim().ToUpperInvariant();

                try
                {
                    var team = await db.Teams
                        .Include(t => t.Roster)
                        .ThenInclude(r => r.Player)
                        .FirstOrDefaultAsync(t => t.TeamId == teamId, ct);

                    if (team is null)
                    {
                        return Results.NotFound(new { message = "Time não encontrado." });
                    }

                    var storedToken = (team.Token ?? string.Empty).Trim().ToUpperInvariant();

                    if (!string.Equals(storedToken, normalizedToken, StringComparison.Ordinal))
                    {
                        return Results.Json(new { message = "Token inválido para este time." }, statusCode: StatusCodes.Status403Forbidden);
                    }

                    var rosterEntry = team.Roster.FirstOrDefault(r => r.Player.PlayerGuid == playerId);
                    if (rosterEntry is null)
                    {
                        return Results.NotFound(new { message = "Jogador não encontrado no elenco." });
                    }

                    if (team.Roster.Count <= 18)
                    {
                        return Results.Conflict(new { message = "Seu time ficaria com menos de 18 jogadores após a venda." });
                    }

                    PricingResult pricing;
                    try
                    {
                        pricing = await pricingService.CalculateForPlayerAsync(rosterEntry.PlayerId, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Falha ao calcular preço base para o jogador {PlayerId} no Quick Sell.", rosterEntry.PlayerId);
                        return Results.Json(new { message = "Ocorreu um erro ao processar o Quick Sell." }, statusCode: StatusCodes.Status500InternalServerError);
                    }

                    var basePrice = pricing.BasePrice;
                    var payout = decimal.Round(basePrice * 0.8m, 2, MidpointRounding.AwayFromZero);
                    var occurredAtUtc = TimeProvider.System.GetUtcNow().UtcDateTime;
                    var overall = rosterEntry.Player.Overall;

                    await using var transaction = await db.Database.BeginTransactionAsync(ct);
                    try
                    {
                        rosterEntry.Player.CurrentTeamId = null;
                        db.TeamRosters.Remove(rosterEntry);
                        team.Budget += payout;

                        var historyEntry = new TransferHistory
                        {
                            TransferId = Guid.NewGuid(),
                            Type = TransferType.QuickSell,
                            PlayerId = rosterEntry.PlayerId,
                            FromTeamId = team.TeamId,
                            Amount = payout,
                            PerformedAtUtc = occurredAtUtc,
                            OldOverall = overall,
                            NewOverall = overall
                        };

                        await db.TransferHistories.AddAsync(historyEntry, ct);
                        await db.SaveChangesAsync(ct);
                        await transaction.CommitAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync(ct);
                        logger.LogError(ex, "Erro ao persistir Quick Sell para o jogador {PlayerGuid} do time {TeamId}.", rosterEntry.Player.PlayerGuid, team.TeamId);
                        return Results.Json(new { message = "Ocorreu um erro ao processar o Quick Sell." }, statusCode: StatusCodes.Status500InternalServerError);
                    }

                    var result = new QuickSellResultDto(
                        team.TeamId,
                        rosterEntry.Player.PlayerGuid,
                        overall,
                        overall,
                        basePrice,
                        payout,
                        team.Budget,
                        occurredAtUtc);

                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Erro inesperado ao realizar Quick Sell para o time {TeamId} e jogador {PlayerId}.", teamId, playerId);
                    return Results.Json(new { message = "Ocorreu um erro ao processar o Quick Sell." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            // ADMIN
            var adminTeamsApi = api.MapGroup("/admin/teams").RequireAuthorization("AdminOnly");

            adminTeamsApi.MapPost(string.Empty, async (ITeamService teamService, TeamCreateDto dto) =>
            {
                try
                {
                    var id = await teamService.CreateAsync(dto);
                    return Results.Created($"/api/teams/{id}", new { id });
                }
                catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
                catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
            });

            adminTeamsApi.MapPut("/{id:guid}", async (ITeamService teamService, Guid id, TeamUpdateDto dto) =>
            {
                try
                {
                    await teamService.UpdateAsync(id, dto);
                    return Results.NoContent();
                }
                catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
                catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
            });

            adminTeamsApi.MapDelete("/{id:guid}", async (ITeamService teamService, Guid id) =>
            {
                try
                {
                    await teamService.DeleteAsync(id);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException) { return Results.NotFound(); }
                catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
            });

            adminTeamsApi.MapPost("/adjust-budget", async (
                HttpContext httpContext,
                AdminAdjustBudgetRequestDto request,
                AdminTransferService adminTransferService,
                CancellationToken ct) =>
            {
                if (request is null) return Results.BadRequest(new { message = "Payload inválido." });
                if (request.TeamId == Guid.Empty) return Results.BadRequest(new { message = "TeamId é obrigatório." });
                if (request.Delta == 0m) return Results.BadRequest(new { message = "O ajuste deve ser diferente de zero." });

                if (!EndpointHelpers.TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
                    return errorResult!;

                try
                {
                    await adminTransferService.AdjustBudgetAsync(adminToken!, request.TeamId, request.Delta, request.Reason, ct);
                    return Results.Ok(new { message = "Orçamento ajustado com sucesso." });
                }
                catch (AdminForbiddenException ex) { return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden); }
                catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
                catch (KeyNotFoundException ex) { return Results.NotFound(new { message = ex.Message }); }
            });

            return api;
        }
    }
}
