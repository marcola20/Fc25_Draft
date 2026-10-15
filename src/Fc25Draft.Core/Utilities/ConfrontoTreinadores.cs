using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Retrospecto de um técnico contra os outros: cada jogo encerrado vira um duelo entre quem estava no comando
/// de cada lado quando ele começou (<see cref="ComandoDoJogo"/>). O duelo segue a pessoa: vale com qualquer clube
/// que os dois estivessem dirigindo. Quem era auxiliar também entra, mas o jogo fica marcado (o asterisco nas
/// telas), porque o clube tinha um técnico. Pênaltis contam como empate, igual ao confronto dos clubes.
/// </summary>
public static class ConfrontoTreinadores
{
    public sealed record Passagem(Guid TreinadorId, ComandoDoJogo.Periodo Periodo);

    public sealed record Jogo(
        Guid PartidaId,
        string Competicao,
        Guid CasaId,
        string CasaNome,
        Guid ForaId,
        string ForaNome,
        int GolsCasa,
        int GolsFora,
        bool IsWO,
        DateTime Quando);

    /// <summary>Os confrontos do técnico, do adversário mais enfrentado para o menos.</summary>
    public static IReadOnlyList<TreinadorConfrontoDto> Calcular(
        Guid treinadorId,
        IEnumerable<Jogo> jogos,
        IReadOnlyCollection<Passagem> passagens,
        IReadOnlyDictionary<Guid, string> nomes)
    {
        var periodos = passagens.Select(p => p.Periodo).ToList();
        var janelas = passagens
            .Select(p => (p.TreinadorId, p.Periodo.TimeId, p.Periodo.Papel, Janela: ComandoDoJogo.Janela(p.Periodo, periodos)))
            .ToLookup(p => p.TimeId);

        // Técnico e auxiliar que estavam no clube quando o jogo começou.
        List<(Guid Id, bool Auxiliar)> NoBanco(Guid timeId, DateTime quando) =>
            janelas[timeId]
                .Where(j => ComandoDoJogo.Comandou(j.Janela, quando))
                .Select(j => (j.TreinadorId, j.Papel == PapelTreinador.Auxiliar))
                .Distinct()
                .ToList();

        var duelos = new List<(Guid Adversario, TreinadorConfrontoJogoDto Jogo)>();
        foreach (var j in jogos)
        {
            var casa = NoBanco(j.CasaId, j.Quando);
            var fora = NoBanco(j.ForaId, j.Quando);

            var emCasa = casa.FirstOrDefault(c => c.Id == treinadorId);
            var deFora = fora.FirstOrDefault(c => c.Id == treinadorId);
            if (emCasa.Id == Guid.Empty && deFora.Id == Guid.Empty) continue;

            var ehCasa = emCasa.Id != Guid.Empty;
            var ele = ehCasa ? emCasa : deFora;
            foreach (var adv in ehCasa ? fora : casa)
            {
                if (adv.Id == treinadorId) continue;
                duelos.Add((adv.Id, ehCasa
                    ? new TreinadorConfrontoJogoDto(j.PartidaId, j.Quando, j.Competicao, j.CasaNome, j.ForaNome, true,
                        j.GolsCasa, j.GolsFora, j.IsWO, ele.Auxiliar, adv.Auxiliar)
                    : new TreinadorConfrontoJogoDto(j.PartidaId, j.Quando, j.Competicao, j.ForaNome, j.CasaNome, false,
                        j.GolsFora, j.GolsCasa, j.IsWO, ele.Auxiliar, adv.Auxiliar)));
            }
        }

        return duelos
            .GroupBy(d => d.Adversario)
            .Select(g =>
            {
                var lista = g.Select(d => d.Jogo).OrderByDescending(d => d.Data).ToList();
                return new TreinadorConfrontoDto(g.Key, nomes.GetValueOrDefault(g.Key) ?? "?", Somar(lista), lista);
            })
            .OrderByDescending(c => c.Retrospecto.Jogos)
            .ThenByDescending(c => c.Retrospecto.Vitorias)
            .ThenBy(c => c.AdversarioNome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>O técnico que ele mais venceu (pelo menos 2 vitórias e vantagem no duelo).</summary>
    public static TreinadorConfrontoDto? Fregues(IEnumerable<TreinadorConfrontoDto> confrontos) =>
        confrontos
            .Where(c => c.Retrospecto.Vitorias >= 2 && c.Retrospecto.Vitorias > c.Retrospecto.Derrotas)
            .OrderByDescending(c => c.Retrospecto.Vitorias).ThenBy(c => c.Retrospecto.Derrotas)
            .FirstOrDefault();

    /// <summary>O técnico que mais o venceu (pelo menos 2 derrotas e desvantagem no duelo).</summary>
    public static TreinadorConfrontoDto? Carrasco(IEnumerable<TreinadorConfrontoDto> confrontos) =>
        confrontos
            .Where(c => c.Retrospecto.Derrotas >= 2 && c.Retrospecto.Derrotas > c.Retrospecto.Vitorias)
            .OrderByDescending(c => c.Retrospecto.Derrotas).ThenBy(c => c.Retrospecto.Vitorias)
            .FirstOrDefault();

    private static TreinadorRetrospectoDto Somar(IEnumerable<TreinadorConfrontoJogoDto> jogos)
    {
        int j = 0, v = 0, e = 0, d = 0, gp = 0, gc = 0;
        foreach (var jogo in jogos)
        {
            j++;
            gp += jogo.GolsPro;
            gc += jogo.GolsContra;
            if (jogo.GolsPro > jogo.GolsContra) v++;
            else if (jogo.GolsPro < jogo.GolsContra) d++;
            else e++;
        }

        return new TreinadorRetrospectoDto(j, v, e, d, gp, gc);
    }
}
