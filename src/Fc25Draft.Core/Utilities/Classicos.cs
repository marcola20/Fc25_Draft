using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Clássico é o jogo entre dois treinadores rivais, cada um no comando de um dos clubes (técnico principal dos dois
/// lados; auxiliar não conta). A rivalidade segue as pessoas: vale com qualquer clube. Quem comandava cada lado é quem
/// estava lá quando o jogo começou (<see cref="ComandoDoJogo"/>); jogo ainda não jogado usa quem está lá agora.
/// O vencedor de um clássico que começou depois que a rivalidade foi criada leva o prêmio para o clube que comandava
/// no jogo. Empate (pênaltis contam como empate) e W.O. não pagam.
/// </summary>
public static class Classicos
{
    public const decimal PremioPadrao = 3_000_000m;

    public sealed record Rival(Guid RivalidadeId, Guid A, Guid B, string? Nome, DateTime CriadaEm);

    public sealed record Jogo(Guid PartidaId, Guid CasaId, Guid ForaId, DateTime Quando);

    /// <summary>O clássico daquele jogo: a rivalidade e o técnico de cada lado.</summary>
    public sealed record DoJogo(Guid PartidaId, Rival Rival, Guid TecnicoCasaId, Guid TecnicoForaId);

    /// <summary>O par sempre na mesma ordem (menor primeiro), para não ter A x B e B x A.</summary>
    public static (Guid A, Guid B) Par(Guid x, Guid y) => x.CompareTo(y) < 0 ? (x, y) : (y, x);

    /// <summary>Quais dos jogos são clássicos, pelo id da partida.</summary>
    public static IReadOnlyDictionary<Guid, DoJogo> DosJogos(
        IEnumerable<Jogo> jogos,
        IReadOnlyCollection<ConfrontoTreinadores.Passagem> passagens,
        IEnumerable<Rival> rivais)
    {
        var porPar = rivais.ToDictionary(r => Par(r.A, r.B));
        var resultado = new Dictionary<Guid, DoJogo>();
        if (porPar.Count == 0) return resultado;

        var periodos = passagens.Select(p => p.Periodo).ToList();
        var janelas = passagens
            .Where(p => p.Periodo.Papel == PapelTreinador.Treinador)
            .Select(p => (p.TreinadorId, p.Periodo.TimeId, Janela: ComandoDoJogo.Janela(p.Periodo, periodos)))
            .ToLookup(p => p.TimeId);

        Guid? Tecnico(Guid timeId, DateTime quando) =>
            janelas[timeId].Where(j => ComandoDoJogo.Comandou(j.Janela, quando)).Select(j => (Guid?)j.TreinadorId).FirstOrDefault();

        foreach (var j in jogos)
        {
            if (Tecnico(j.CasaId, j.Quando) is not Guid casa || Tecnico(j.ForaId, j.Quando) is not Guid fora || casa == fora)
                continue;
            if (porPar.TryGetValue(Par(casa, fora), out var rival))
                resultado[j.PartidaId] = new DoJogo(j.PartidaId, rival, casa, fora);
        }

        return resultado;
    }

    /// <summary>
    /// Retrospecto de A contra B só nos clássicos (os dois de técnico principal), a partir dos confrontos de A
    /// (<see cref="ConfrontoTreinadores.Calcular"/>), com os jogos do mais recente para o mais antigo.
    /// </summary>
    public static (TreinadorRetrospectoDto Retrospecto, IReadOnlyList<TreinadorConfrontoJogoDto> Jogos) Retrospecto(
        IEnumerable<TreinadorConfrontoDto> confrontosDeA, Guid treinadorB)
    {
        var jogos = confrontosDeA
            .Where(c => c.AdversarioId == treinadorB)
            .SelectMany(c => c.Jogos)
            .Where(j => !j.ComAuxiliar)
            .OrderByDescending(j => j.Data)
            .ToList();

        int v = 0, e = 0, d = 0, gp = 0, gc = 0;
        foreach (var j in jogos)
        {
            gp += j.GolsPro;
            gc += j.GolsContra;
            if (j.GolsPro > j.GolsContra) v++;
            else if (j.GolsPro < j.GolsContra) d++;
            else e++;
        }

        return (new TreinadorRetrospectoDto(jogos.Count, v, e, d, gp, gc), jogos);
    }

    /// <summary>Quem venceu no tempo normal: o técnico da casa, o de fora, ou ninguém (empate/W.O.).</summary>
    public static Guid? Vencedor(DoJogo classico, int golsCasa, int golsFora, bool isWO) =>
        isWO || golsCasa == golsFora ? null
        : golsCasa > golsFora ? classico.TecnicoCasaId
        : classico.TecnicoForaId;

    /// <summary>"3 Mi", "2,5 Mi".</summary>
    public static string Rotulo(decimal valor) =>
        $"{(valor / 1_000_000m).ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} Mi";
}
