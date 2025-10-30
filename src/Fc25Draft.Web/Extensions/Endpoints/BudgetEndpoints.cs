using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fc25Draft.Infra.Data;
using Fc25Draft.Core.DTOs;          // BudgetSummaryDto, LedgerItemDto, PagedResult<T>
using Fc25Draft.Core.Interfaces;    // IBudgetService

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class BudgetEndpoints
    {
        public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder api)
        {
            var budgetApi = api.MapGroup("/budgets");

            budgetApi.MapGet(
                "/available",
                async ([FromQuery] string? token, DraftDbContext db, IBudgetService budgetService, CancellationToken ct) =>
                {
                    if (string.IsNullOrWhiteSpace(token))
                        return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);

                    var teamId = await db.Teams
                        .AsNoTracking()
                        .Where(t => t.Token == token.Trim())
                        .Select(t => (Guid?)t.TeamId)
                        .FirstOrDefaultAsync(ct);

                    if (!teamId.HasValue)
                        return Results.Json(new { message = "Token inválido." }, statusCode: StatusCodes.Status401Unauthorized);

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
                        return Results.BadRequest(new { message = "TeamId inválido." });

                    var teamExists = await db.Teams.AsNoTracking().AnyAsync(t => t.TeamId == teamId, ct);
                    if (!teamExists)
                        return Results.NotFound(new { message = $"Time {teamId} não encontrado." });

                    var saldo = await budgetService.GetSaldoAsync(teamId, ct);
                    return Results.Ok(new { teamId, saldo });
                })
                .AllowAnonymous();

            // ADMIN
            var adminBudgetApi = api.MapGroup("/admin/budgets").RequireAuthorization("AdminOnly");

            adminBudgetApi.MapPost(
                "/adjust",
                async (BudgetAdjustRequestDto request, IBudgetService budgetService, CancellationToken ct) =>
                {
                    if (request is null) return Results.BadRequest(new { message = "Payload inválido." });
                    if (request.TeamId == Guid.Empty) return Results.BadRequest(new { message = "TeamId é obrigatório." });
                    if (request.Valor <= 0) return Results.BadRequest(new { message = "Valor deve ser maior que zero." });
                    if (string.IsNullOrWhiteSpace(request.Origem)) return Results.BadRequest(new { message = "Origem é obrigatória." });

                    var tipo = request.Tipo?.Trim().ToUpperInvariant();
                    if (tipo is not ("CREDIT" or "DEBIT"))
                        return Results.BadRequest(new { message = "Tipo inválido. Use CREDIT ou DEBIT." });

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
                    catch (KeyNotFoundException ex) { return Results.NotFound(new { message = ex.Message }); }
                    catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
                });

            adminBudgetApi.MapPost(
                "/apply-match-reward",
                async (MatchRewardRequestDto request, IBudgetService budgetService, CancellationToken ct) =>
                {
                    if (request is null) return Results.BadRequest(new { message = "Payload inválido." });

                    try
                    {
                        var result = await budgetService.ApplyMatchRewardAsync(request, ct);

                        if (!result.AjusteRealizado)
                            return Results.Ok(new { message = "Sem alteração.", teamId = result.TeamId, saldo = result.SaldoAtual });

                        return Results.Ok(new
                        {
                            teamId = result.TeamId,
                            valorAplicado = result.ValorAplicado,
                            saldo = result.SaldoAtual,
                            tipo = result.Tipo,
                            descricao = result.Descricao
                        });
                    }
                    catch (KeyNotFoundException ex) { return Results.NotFound(new { message = ex.Message }); }
                    catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
                });

            adminBudgetApi.MapGet(
                "/ledger",
                async ([FromQuery] Guid teamId, DraftDbContext db, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
                {
                    if (teamId == Guid.Empty) return Results.BadRequest(new { message = "teamId é obrigatório." });
                    if (page < 1 || pageSize < 1) return Results.BadRequest(new { message = "Parâmetros de paginação inválidos." });

                    var size = Math.Min(pageSize, 100);

                    var teamExists = await db.Teams.AsNoTracking().AnyAsync(t => t.TeamId == teamId, ct);
                    if (!teamExists) return Results.NotFound(new { message = $"Time {teamId} não encontrado." });

                    var query = db.BudgetLedgers.AsNoTracking().Where(l => l.TeamId == teamId);

                    var total = await query.CountAsync(ct);
                    var items = await query
                        .OrderByDescending(l => l.DataUtc)
                        .Skip((page - 1) * size)
                        .Take(size)
                        .Select(l => new LedgerItemDto(l.DataUtc, l.Tipo, l.Origem, l.Valor, l.Descricao))
                        .ToListAsync(ct);

                    return Results.Ok(new PagedResult<LedgerItemDto>(items, total));
                });

            return api;
        }
    }
}
