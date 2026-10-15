using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Retrospecto de um técnico contra os outros: cada jogo encerrado vira um duelo entre quem estava no comando
/// de cada lado quando ele começou (<see cref="ComandoDoJogo"/>). Conta só o papel de treinador, e o duelo
/// segue a pessoa: vale com qualquer clube que os dois estivessem dirigindo. Pênaltis contam como empate,
/// igual ao confronto dos clubes.
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
        var tecnicos = passagens.Where(p => p.Periodo.Papel == PapelTreinador.Treinador).ToList();
        var periodos = tecnicos.Select(p => p.Periodo).ToList();
        var janelas = tecnicos
            .Select(p => (p.TreinadorId, p.Periodo.TimeId, Janela: ComandoDoJogo.Janela(p.Periodo, periodos)))
            .ToLookup(p => p.TimeId);

        Guid? Comandava(Guid timeId, DateTime quando) =>
            janelas[timeId].Where(j => ComandoDoJogo.Comandou(j.Janela, quando)).Select(j => (Guid?)j.TreinadorId).FirstOrDefault();

        var duelos = new List<(Guid Adversario, TreinadorConfrontoJogoDto Jogo)>();
        foreach (var j in jogos)
        {
            var casa = Comandava(j.CasaId, j.Quando);
            var fora = Comandava(j.ForaId, j.Quando);
            if (casa is null || fora is null || casa == fora) continue;

            if (casa == treinadorId)
                duelos.Add((fora.Value, new TreinadorConfrontoJogoDto(
                    j.PartidaId, j.Quando, j.Competicao, j.CasaNome, j.ForaNome, true, j.GolsCasa, j.GolsFora, j.IsWO)));
            else if (fora == treinadorId)
                duelos.Add((casa.Value, new TreinadorConfrontoJogoDto(
                    j.PartidaId, j.Quando, j.Competicao, j.ForaNome, j.CasaNome, false, j.GolsFora, j.GolsCasa, j.IsWO)));
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
