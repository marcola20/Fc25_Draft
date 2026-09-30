using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Extensions.Endpoints
{
    /// <summary>Endpoints usados pelo Editor PES (programa no PC do admin que edita o save do jogo).</summary>
    public static class PesEndpoints
    {
        public static IEndpointRouteBuilder MapPesEndpoints(this IEndpointRouteBuilder routes)
        {
            var pes = routes.MapGroup("/admin/pes").RequireAuthorization("AdminOnly");

            // Todos os jogadores com o ID do PES ligado, numa chamada só.
            pes.MapGet("/ligacoes", async (DraftDbContext db, CancellationToken ct) =>
                Results.Ok(await db.Players
                    .AsNoTracking()
                    .OrderBy(p => p.PlayerId)
                    .Select(p => new LigacaoPesDto(
                        p.PlayerId,
                        p.Name,
                        p.PositionId,
                        p.Age,
                        p.Overall,
                        p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                        p.Atributos == null ? null : p.Atributos.PesId))
                    .ToListAsync(ct)));

            // Grava as ligações conferidas no editor; os atributos vêm da base do PES embutida.
            // ?atualizarAtributos=true: quem já estava ligado também recebe os atributos da base.
            pes.MapPut("/ligacoes", async (IBasePesService basePes, List<DefinirLigacaoPesDto> itens,
                bool? atualizarAtributos, CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await basePes.DefinirLigacoesAsync(itens, atualizarAtributos ?? false, ct));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            });

            // Evoluções feitas no site (venda rápida) que o editor ainda tem que gravar no save.
            pes.MapGet("/evolucoes", async (ISincronizacaoPesService sync, CancellationToken ct) =>
                Results.Ok(await sync.EvolucoesPendentesAsync(ct)));

            // Depois de gravar o save: confirma as evoluções aplicadas e manda os atributos que ficaram no jogo.
            pes.MapPost("/sincronizar", async (ISincronizacaoPesService sync, SincronizarPesDto dados, CancellationToken ct) =>
            {
                try
                {
                    return Results.Ok(await sync.SincronizarAsync(dados, ct));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            });

            // Overall de todos pela fórmula do PES. ?gravar=true grava; sem ele, só mostra o que mudaria.
            pes.MapPost("/recalcular-overall", async (ISincronizacaoPesService sync, bool? gravar, CancellationToken ct) =>
                Results.Ok(await sync.RecalcularTodosAsync(gravar ?? false, ct)));

            return routes;
        }
    }
}
