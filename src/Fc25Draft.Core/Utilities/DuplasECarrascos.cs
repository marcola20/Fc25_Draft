using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Utilities;

/// <summary>Um gol (sem os contra): TimeId é o time de quem marcou.</summary>
public record LanceGolInput(
    Guid PartidaId, Guid TimeId, int JogadorId, string JogadorNome,
    int? AssistenteId, string? AssistenteNome, int? Minuto, DateTime CriadoEm);

/// <summary>Um jogo com gol, com o nome da competição e o rótulo da etapa.</summary>
public record JogoGolInput(
    Guid PartidaId, string Competicao, string Rotulo, Guid CasaId, Guid ForaId, int GolsCasa, int GolsFora, DateTime? Quando);

/// <summary>Duplas que mais combinaram em gol e jogadores que mais marcaram contra um mesmo time.</summary>
public static class DuplasECarrascos
{
    public static IReadOnlyList<DuplaDto> Duplas(
        IEnumerable<LanceGolInput> gols, IReadOnlyCollection<JogoGolInput> jogos, IReadOnlyDictionary<Guid, string> nomes)
    {
        var porJogo = jogos.ToDictionary(j => j.PartidaId);

        return EmOrdem(gols.Where(g => g.AssistenteId is int a && a != g.JogadorId && porJogo.ContainsKey(g.PartidaId)), porJogo)
            .GroupBy(g => (Math.Min(g.JogadorId, g.AssistenteId!.Value), Math.Max(g.JogadorId, g.AssistenteId!.Value)))
            .Select(g =>
            {
                // A é quem mais serviu o outro (no empate, o de nome menor).
                var (x, y) = g.Key;
                var xServiu = g.Count(l => l.AssistenteId == x);
                var yServiu = g.Count() - xServiu;
                var nomeX = Nome(g, x);
                var nomeY = Nome(g, y);
                var xPrimeiro = xServiu > yServiu
                    || (xServiu == yServiu && StringComparer.OrdinalIgnoreCase.Compare(nomeX, nomeY) <= 0);
                var (a, nomeA, b, nomeB) = xPrimeiro ? (x, nomeX, y, nomeY) : (y, nomeY, x, nomeX);
                var ultimo = g.Last();

                return new DuplaDto(a, nomeA, b, nomeB, ultimo.TimeId, nomes.GetValueOrDefault(ultimo.TimeId, "?"),
                    g.Count(l => l.AssistenteId == a), g.Count(l => l.AssistenteId == b),
                    g.Select(l => new DuplaGolDto(Resumo(porJogo[l.PartidaId], nomes), l.Minuto, l.AssistenteId == a)).ToList());
            })
            .OrderByDescending(d => d.Total)
            .ThenByDescending(d => Math.Max(d.AParaB, d.BParaA))
            .ThenBy(d => d.JogadorANome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<CarrascoDto> Carrascos(
        IEnumerable<LanceGolInput> gols, IReadOnlyCollection<JogoGolInput> jogos, IReadOnlyDictionary<Guid, string> nomes)
    {
        var porJogo = jogos.ToDictionary(j => j.PartidaId);
        Guid Adversario(LanceGolInput g) => porJogo[g.PartidaId].CasaId == g.TimeId ? porJogo[g.PartidaId].ForaId : porJogo[g.PartidaId].CasaId;

        return EmOrdem(gols.Where(g => porJogo.ContainsKey(g.PartidaId)), porJogo)
            .GroupBy(g => (g.JogadorId, Adversario: Adversario(g)))
            .Select(g => new CarrascoDto(
                g.Key.JogadorId, g.Last().JogadorNome, g.Key.Adversario, nomes.GetValueOrDefault(g.Key.Adversario, "?"), g.Count(),
                g.GroupBy(l => l.PartidaId)
                    .Select(j => new CarrascoJogoDto(
                        Resumo(porJogo[j.Key], nomes),
                        nomes.GetValueOrDefault(j.First().TimeId, "?"),
                        j.Count(),
                        j.Where(l => l.Minuto is not null).Select(l => l.Minuto!.Value).ToList()))
                    .ToList()))
            .OrderByDescending(c => c.Gols)
            .ThenBy(c => c.Jogos.Count)
            .ThenBy(c => c.JogadorNome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Na ordem em que aconteceram: jogo mais antigo primeiro, depois o minuto.</summary>
    private static IEnumerable<LanceGolInput> EmOrdem(IEnumerable<LanceGolInput> gols, IReadOnlyDictionary<Guid, JogoGolInput> porJogo) =>
        gols.OrderBy(g => porJogo[g.PartidaId].Quando ?? DateTime.MinValue)
            .ThenBy(g => g.Minuto ?? int.MaxValue)
            .ThenBy(g => g.CriadoEm);

    private static string Nome(IEnumerable<LanceGolInput> lances, int jogadorId) =>
        lances.Select(l => l.JogadorId == jogadorId ? l.JogadorNome : l.AssistenteId == jogadorId ? l.AssistenteNome : null)
            .LastOrDefault(n => n is not null) ?? "?";

    private static JogoResumoDto Resumo(JogoGolInput j, IReadOnlyDictionary<Guid, string> nomes) =>
        new(j.PartidaId, j.Competicao, j.Rotulo, nomes.GetValueOrDefault(j.CasaId, "?"), nomes.GetValueOrDefault(j.ForaId, "?"),
            j.GolsCasa, j.GolsFora);
}
