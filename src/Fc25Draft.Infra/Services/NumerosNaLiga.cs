using Fc25Draft.Core.Enums;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Jogos, gols e assistências de cada jogador na liga inteira (todas as competições).</summary>
internal static class NumerosNaLiga
{
    public sealed record Numeros(int Jogos, int Gols, int Assistencias);

    public static async Task<Dictionary<int, Numeros>> CalcularAsync(DraftDbContext db, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new();

        var golsEAssist = await db.LigaEventos.AsNoTracking()
            .Where(e => e.Tipo == TipoEvento.Gol && (ids.Contains(e.JogadorId) || (e.AssistenteId != null && ids.Contains(e.AssistenteId.Value))))
            .Select(e => new { e.JogadorId, e.AssistenteId })
            .ToListAsync(ct);
        var gols = golsEAssist.GroupBy(e => e.JogadorId).ToDictionary(g => g.Key, g => g.Count());
        var assist = golsEAssist.Where(e => e.AssistenteId != null).GroupBy(e => e.AssistenteId!.Value).ToDictionary(g => g.Key, g => g.Count());

        // Jogos: começou como titular ou ganhou nota do PES (quem entrou do banco também tem nota).
        var titular = await db.LigaEscalacoes.AsNoTracking()
            .Where(e => ids.Contains(e.JogadorId) && e.Titular)
            .Select(e => new { e.JogadorId, e.PartidaId })
            .ToListAsync(ct);
        var comNota = await db.LigaNotasJogadores.AsNoTracking()
            .Where(n => ids.Contains(n.JogadorId))
            .Select(n => new { n.JogadorId, n.PartidaId })
            .ToListAsync(ct);
        var jogos = titular.Concat(comNota).Distinct()
            .GroupBy(x => x.JogadorId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Mais os jogos anotados à mão nas competições anteriores ao contador.
        var anotados = await db.JogosAnotados.AsNoTracking()
            .Where(j => ids.Contains(j.JogadorId) && !db.LigaEscalacoes.Any(e => e.Partida.Rodada.LigaId == j.LigaId))
            .GroupBy(j => j.JogadorId)
            .Select(g => new { g.Key, Jogos = g.Sum(j => j.Jogos) })
            .ToListAsync(ct);
        foreach (var a in anotados)
            jogos[a.Key] = jogos.GetValueOrDefault(a.Key) + a.Jogos;

        return ids.Distinct().ToDictionary(id => id, id => new Numeros(
            jogos.GetValueOrDefault(id), gols.GetValueOrDefault(id), assist.GetValueOrDefault(id)));
    }
}
