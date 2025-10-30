using Fc25Draft.Core.DTOs;
using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class DraftEndpoints
    {
        /// <summary>
        /// Registra todos os endpoints do módulo Draft em /api/draft e /api/admin/draft
        /// </summary>
        public static IEndpointRouteBuilder MapDraftEndpoints(this IEndpointRouteBuilder api)
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
                        p.Player != null ? p.Player.PositionId : null,
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
                    return Results.File(System.Text.Encoding.UTF8.GetBytes(emptyCsv), "text/csv", "draft-board.csv");
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
                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                return Results.File(bytes, "text/csv", "draft-board.csv");
            });

           
            draftApi.RequireAuthorization(new AuthorizeAttribute[] { }); 

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

            return api;
        }


        private static string BuildDraftBoardCsv(IReadOnlyList<DraftBoardExportDto> entries)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Rodada;Escolha;Time;Responsável;Jogador;Posição;Data/Hora");
            foreach (var entry in entries)
            {
                sb.AppendLine($"{entry.Rodada};{entry.Escolha};{Escape(entry.Time)};{Escape(entry.Responsavel)};{Escape(entry.Jogador)};{Escape(entry.Posicao)};{Escape(entry.DataHoraUtc)}");
            }
            return sb.ToString();
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sanitized = value.Replace("\"", "''");
            return sanitized.Contains(';') ? $"\"{sanitized}\"" : sanitized;
        }
    }
}
