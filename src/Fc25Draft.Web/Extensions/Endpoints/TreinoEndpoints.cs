using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;

namespace Fc25Draft.Web.Extensions.Endpoints;

/// <summary>Treino da semana: o time (pelo token) troca as escolhas desta semana.</summary>
public static class TreinoEndpoints
{
    public static IEndpointRouteBuilder MapTreinoEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapPut("/treino", HandleSalvarAsync);
        api.MapPost("/treino/padrinho", HandleApadrinharAsync);
        api.MapPost("/comissao/{cargo}", HandleContratarAsync);
        return api;
    }

    private static async Task<IResult> HandleSalvarAsync(
        IReadOnlyList<TreinoEscolhaRequest> escolhas,
        ITreinoService service,
        DraftDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var token = httpContext.Request.Headers["X-Team-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token) || await db.TimeIdPorTokenAsync(token, ct) is not Guid teamId)
            return Results.Unauthorized();

        try
        {
            await service.SalvarAsync(teamId, escolhas, ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> HandleContratarAsync(
        CargoDaComissao cargo,
        IComissaoService service,
        DraftDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var token = httpContext.Request.Headers["X-Team-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token) || await db.TimeIdPorTokenAsync(token, ct) is not Guid teamId)
            return Results.Unauthorized();

        try
        {
            await service.ContratarAsync(teamId, cargo, ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> HandleApadrinharAsync(
        ApadrinharRequest pedido,
        ITreinoService service,
        DraftDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var token = httpContext.Request.Headers["X-Team-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token) || await db.TimeIdPorTokenAsync(token, ct) is not Guid teamId)
            return Results.Unauthorized();

        try
        {
            await service.ApadrinharAsync(teamId, pedido.PadrinhoId, pedido.AfilhadoId, ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }
}
