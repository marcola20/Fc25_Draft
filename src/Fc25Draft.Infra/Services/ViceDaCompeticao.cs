using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Quem ficou com o vice de uma competição encerrada e quem dirigia o time naquela hora. Com final
/// (mata-mata da Copa, da liga antiga ou jogo da Supercopa) é quem perdeu a final; na liga de pontos
/// corridos, o 2º da tabela.
/// </summary>
internal static class ViceDaCompeticao
{
    public sealed record Vice(Guid TimeId, string Time, Guid? TreinadorId, string? Tecnico);

    public static async Task<Vice?> CalcularAsync(DraftDbContext db, Guid ligaId, Guid campeaoId, CancellationToken ct)
    {
        var tipo = await db.Ligas.AsNoTracking().Where(l => l.LigaId == ligaId).Select(l => (TipoCompetition?)l.Tipo).FirstOrDefaultAsync(ct);
        if (tipo is null) return null;

        Guid? viceId = null;

        var final = await db.LigaKnockoutJogos.AsNoTracking()
            .Where(k => k.LigaId == ligaId && k.Fase == FaseKnockout.Final)
            .Select(k => new { k.TimeCasaId, k.TimeForaId })
            .FirstOrDefaultAsync(ct);
        if (final is { TimeCasaId: Guid casa, TimeForaId: Guid fora } && (casa == campeaoId || fora == campeaoId))
            viceId = casa == campeaoId ? fora : casa;

        // Supercopa (e Copa sem chave cadastrada): o último jogo do campeão é a final.
        if (viceId is null && tipo != TipoCompetition.Liga)
        {
            var ultimo = await db.LigaPartidas.AsNoTracking()
                .Where(p => p.Rodada.LigaId == ligaId && (p.TimeCasaId == campeaoId || p.TimeForaId == campeaoId))
                .OrderByDescending(p => p.Rodada.Numero)
                .Select(p => new { p.TimeCasaId, p.TimeForaId })
                .FirstOrDefaultAsync(ct);
            if (ultimo is not null)
                viceId = ultimo.TimeCasaId == campeaoId ? ultimo.TimeForaId : ultimo.TimeCasaId;
        }

        if (viceId is null && tipo == TipoCompetition.Liga)
        {
            viceId = await db.LigaClassificacoes.AsNoTracking()
                .Where(c => c.LigaId == ligaId && c.Posicao > 0 && c.TimeId != campeaoId)
                .OrderBy(c => c.Posicao)
                .Select(c => (Guid?)c.TimeId)
                .FirstOrDefaultAsync(ct);
        }

        if (viceId is not Guid id || id == Guid.Empty) return null;

        var time = await db.Teams.AsNoTracking().Where(t => t.TeamId == id).Select(t => t.TeamName).FirstOrDefaultAsync(ct);
        if (time is null) return null;

        // Quem comandava o vice no último jogo da competição; sem data, quem está lá agora.
        var ultimoJogo = await db.LigaPartidas.AsNoTracking()
            .Where(p => p.Rodada.LigaId == ligaId && (p.IniciadaEm != null || p.EncerradaEm != null))
            .MaxAsync(p => p.IniciadaEm ?? p.EncerradaEm, ct);

        var passagens = await db.TreinadorPassagens.AsNoTracking()
            .Where(p => p.TimeId == id && p.Papel == PapelTreinador.Treinador)
            .Select(p => new { Periodo = new ComandoDoJogo.Periodo(p.PassagemId, p.TimeId, p.Papel, p.Desde, p.Ate, p.SaiuEm), p.TreinadorId, p.Treinador.Nome })
            .ToListAsync(ct);

        var periodos = passagens.Select(p => p.Periodo).ToList();
        var tecnico = ultimoJogo is DateTime quando
            ? passagens.FirstOrDefault(p => ComandoDoJogo.Comandou(ComandoDoJogo.Janela(p.Periodo, periodos), quando))
            : null;
        tecnico ??= passagens.Where(p => p.Periodo.Ate == null).OrderByDescending(p => p.Periodo.Desde).FirstOrDefault();

        return new Vice(id, time, tecnico?.TreinadorId, tecnico?.Nome);
    }
}
