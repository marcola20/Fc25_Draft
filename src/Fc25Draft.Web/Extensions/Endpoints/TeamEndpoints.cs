using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;

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
                        t.Roster.Count,
                        t.AuxiliarName))
                    .ToListAsync(ct);

                return Results.Ok(new PagedResult<TeamListItemDto>(items, total, currentPage, currentPageSize));
            });

            teamsApi.MapGet("/budgets", async (DraftDbContext db, CancellationToken ct = default) =>
            {
                var items = await db.Teams
                    .AsNoTracking()
                    .OrderByDescending(t => t.Budget)
                    .ThenBy(t => t.TeamName)
                    .Select(t => new TeamBudgetDto(
                        t.TeamId,
                        t.TeamName,
                        t.OwnerName,
                        t.Budget))
                    .ToListAsync(ct);

                return Results.Ok(items);
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
                        t.AuxiliarName,
                        t.AuxToken,
                        Jogadores = t.Roster.Count,
                        t.Budget,
                        t.QuickSellCount,
                        t.TransferCount
                    })
                    .FirstOrDefaultAsync(ct);

                if (team is null) return Results.NotFound();

                var includeToken = httpContext.User.IsInRole("Admin");
                var teamToken = includeToken ? team.Token : string.Empty;
                var auxToken = includeToken ? team.AuxToken : null;
                var budgetFormatado = string.Format(new System.Globalization.CultureInfo("pt-BR"), "{0:C}", team.Budget);

                var dto = new TeamDetailsDto(team.TeamId, team.TeamName, team.OwnerName, teamToken, team.Jogadores, budgetFormatado, team.QuickSellCount, team.TransferCount, team.AuxiliarName, auxToken);
                return Results.Ok(dto);
            });

            teamsApi.MapGet("/me", async (DraftDbContext db, HttpContext httpContext, CancellationToken ct) =>
            {
                var token = httpContext.Request.Headers["X-Team-Token"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(token))
                    return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);

                var normalized = token.Trim();

                var identity = await db.Teams
                    .AsNoTracking()
                    .Where(t => t.Token == normalized || t.AuxToken == normalized)
                    .Select(t => new TeamIdentityDto(t.TeamId, t.TeamName))
                    .FirstOrDefaultAsync(ct);

                if (identity is null)
                    return Results.Json(new { message = "Token inválido." }, statusCode: StatusCodes.Status403Forbidden);

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
                                r.Player.PositionId,
                                r.Player.Name,
                                r.Player.Position.Name,
                                r.Player.Overall,
                                r.Player.Age,
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => p.PickedAtUtc).FirstOrDefault(),
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => (int?)p.RoundNumber).FirstOrDefault(),
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => (int?)p.PickInRound).FirstOrDefault()))
                            .ToList(),
                        t.AuxiliarName))
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
                                r.Player.PositionId,
                                r.Player.Name,
                                r.Player.Position.Name,
                                r.Player.Overall,
                                r.Player.Age,
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => p.PickedAtUtc).FirstOrDefault(),
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => (int?)p.RoundNumber).FirstOrDefault(),
                                db.DraftPicks.Where(p => p.PlayerId == r.PlayerId).Select(p => (int?)p.PickInRound).FirstOrDefault()))
                            .ToList(),
                        t.AuxiliarName))
                    .FirstOrDefaultAsync(ct);

                return roster is null ? Results.NotFound() : Results.Ok(roster);
            });

            var lineupsApi = teamsApi.MapGroup("/{teamId:guid}/lineups");

            lineupsApi.MapGet(string.Empty, async (
                Guid teamId,
                HttpContext httpContext,
                DraftDbContext db,
                ITeamLineupService lineupService,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("TeamLineupsEndpoints");

                var authorizationResult = await EnsureTeamLineupAccessAsync(db, httpContext, teamId, ct);
                if (authorizationResult is not null)
                {
                    return authorizationResult;
                }

                try
                {
                    var lineups = await lineupService.GetLineupsAsync(teamId, ct);
                    return Results.Ok(lineups);
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning(ex, "Lineups:Get not found {TeamId}", teamId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Lineups:Get invalid request for team {TeamId}", teamId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lineups:Get unexpected error for team {TeamId}", teamId);
                    return Results.Json(new { message = "Não foi possível carregar as escalações." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            lineupsApi.MapPost(string.Empty, async (
                Guid teamId,
                TeamLineupSaveRequestDto request,
                HttpContext httpContext,
                DraftDbContext db,
                ITeamLineupService lineupService,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("TeamLineupsEndpoints");

                var authorizationResult = await EnsureTeamLineupAccessAsync(db, httpContext, teamId, ct);
                if (authorizationResult is not null)
                {
                    return authorizationResult;
                }

                try
                {
                    var result = await lineupService.CreateLineupAsync(teamId, request, ct);
                    return Results.Ok(result);
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning(ex, "Lineups:Create team not found {TeamId}", teamId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Lineups:Create invalid request for team {TeamId}", teamId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lineups:Create unexpected error for team {TeamId}", teamId);
                    return Results.Json(new { message = "Não foi possível salvar a escalação." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            lineupsApi.MapPut("/{lineupId:guid}", async (
                Guid teamId,
                Guid lineupId,
                TeamLineupSaveRequestDto request,
                HttpContext httpContext,
                DraftDbContext db,
                ITeamLineupService lineupService,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("TeamLineupsEndpoints");

                var authorizationResult = await EnsureTeamLineupAccessAsync(db, httpContext, teamId, ct);
                if (authorizationResult is not null)
                {
                    return authorizationResult;
                }

                try
                {
                    var result = await lineupService.UpdateLineupAsync(teamId, lineupId, request, ct);
                    return Results.Ok(result);
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning(ex, "Lineups:Update not found {TeamId}/{LineupId}", teamId, lineupId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Lineups:Update invalid request for team {TeamId}", teamId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lineups:Update unexpected error for team {TeamId} lineup {LineupId}", teamId, lineupId);
                    return Results.Json(new { message = "Não foi possível salvar a escalação." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            lineupsApi.MapDelete("/{lineupId:guid}", async (
                Guid teamId,
                Guid lineupId,
                HttpContext httpContext,
                DraftDbContext db,
                ITeamLineupService lineupService,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("TeamLineupsEndpoints");

                var authorizationResult = await EnsureTeamLineupAccessAsync(db, httpContext, teamId, ct);
                if (authorizationResult is not null)
                {
                    return authorizationResult;
                }

                try
                {
                    await lineupService.DeleteLineupAsync(teamId, lineupId, ct);
                    return Results.Ok(new OperationResultDto(true, "Escalação excluída com sucesso."));
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning(ex, "Lineups:Delete not found {TeamId}/{LineupId}", teamId, lineupId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Lineups:Delete invalid request for team {TeamId}", teamId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lineups:Delete unexpected error for team {TeamId} lineup {LineupId}", teamId, lineupId);
                    return Results.Json(new { message = "Não foi possível excluir a escalação." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            lineupsApi.MapPost("/{lineupId:guid}/activate", async (
                Guid teamId,
                Guid lineupId,
                HttpContext httpContext,
                DraftDbContext db,
                ITeamLineupService lineupService,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("TeamLineupsEndpoints");

                var authorizationResult = await EnsureTeamLineupAccessAsync(db, httpContext, teamId, ct);
                if (authorizationResult is not null)
                {
                    return authorizationResult;
                }

                try
                {
                    await lineupService.SetActiveLineupAsync(teamId, lineupId, ct);
                    return Results.Ok(new OperationResultDto(true, "Escalação definida como ativa."));
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning(ex, "Lineups:Activate not found {TeamId}/{LineupId}", teamId, lineupId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Lineups:Activate invalid request for team {TeamId}", teamId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lineups:Activate unexpected error for team {TeamId} lineup {LineupId}", teamId, lineupId);
                    return Results.Json(new { message = "Não foi possível ativar a escalação." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            lineupsApi.MapPost("/{lineupId:guid}/duplicate", async (
                Guid teamId,
                Guid lineupId,
                HttpContext httpContext,
                DraftDbContext db,
                ITeamLineupService lineupService,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("TeamLineupsEndpoints");

                var authorizationResult = await EnsureTeamLineupAccessAsync(db, httpContext, teamId, ct);
                if (authorizationResult is not null)
                {
                    return authorizationResult;
                }

                try
                {
                    var result = await lineupService.DuplicateLineupAsync(teamId, lineupId, ct);
                    return Results.Ok(result);
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning(ex, "Lineups:Duplicate not found {TeamId}/{LineupId}", teamId, lineupId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning(ex, "Lineups:Duplicate invalid request for team {TeamId}", teamId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lineups:Duplicate unexpected error for team {TeamId} lineup {LineupId}", teamId, lineupId);
                    return Results.Json(new { message = "Não foi possível duplicar a escalação." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            teamsApi.MapPost("/{teamId:guid}/quick-sell/{playerId:guid}", async (
                Guid teamId,
                Guid playerId,
                HttpContext httpContext,
                ITeamQuickSellService quickSellService,
                CancellationToken ct) =>
            {
                if (teamId == Guid.Empty || playerId == Guid.Empty)
                    return Results.Json(new { message = "Parâmetros inválidos." }, statusCode: StatusCodes.Status400BadRequest);
                

                var token = httpContext.Request.Headers["X-Team-Token"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(token))
                    return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);

                var normalizedToken = token.Trim();
                if (string.IsNullOrWhiteSpace(normalizedToken))
                    return Results.Json(new { message = "Parâmetros inválidos." }, statusCode: StatusCodes.Status400BadRequest);

                try
                {
                    var result = await quickSellService.QuickSellAsync(teamId, playerId, normalizedToken, ct);
                    return Results.Ok(result);
                }
                catch (QuickSellException ex)
                {
                    return Results.Json(new { message = ex.Message }, statusCode: ex.StatusCode);
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
                }
                catch
                {
                    return Results.Json(new { message = "Erro interno no servidor." }, statusCode: StatusCodes.Status500InternalServerError);
                }
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

    private static async Task<IResult?> EnsureTeamLineupAccessAsync(
        DraftDbContext db,
        HttpContext httpContext,
        Guid teamId,
        CancellationToken ct)
    {
        if (httpContext.User?.IsInRole("Admin") == true)
        {
            return null;
        }

        if (teamId == Guid.Empty)
        {
            return Results.Json(new { message = "Time inválido." }, statusCode: StatusCodes.Status400BadRequest);
        }

        var token = httpContext.Request.Headers["X-Team-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.Json(new { message = "Token do time obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var normalized = token.Trim();

        var teamTokens = await db.Teams
            .AsNoTracking()
            .Where(t => t.TeamId == teamId)
            .Select(t => new { t.Token, t.AuxToken })
            .FirstOrDefaultAsync(ct);

        if (teamTokens is null)
        {
            return Results.Json(new { message = "Time não encontrado." }, statusCode: StatusCodes.Status404NotFound);
        }

        var matches = string.Equals(teamTokens.Token, normalized, StringComparison.OrdinalIgnoreCase)
            || (teamTokens.AuxToken is not null && string.Equals(teamTokens.AuxToken, normalized, StringComparison.OrdinalIgnoreCase));

        if (!matches)
        {
            return Results.Json(new { message = "Token do time inválido." }, statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }
}
}
