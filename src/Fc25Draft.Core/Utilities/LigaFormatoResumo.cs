using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Descreve o formato de uma edição a partir do que ela realmente teve (times, grupos,
/// rodadas e fases do mata-mata). Cada temporada teve um formato diferente, então o resumo
/// sai dos dados da própria edição em vez de uma descrição fixa da competição atual.
/// </summary>
public static class LigaFormatoResumo
{
    public static IReadOnlyList<string> Itens(LigaEdicaoDto e)
    {
        if (e.Tipo == TipoCompetition.Supercopa)
            return new[] { "Jogo único", "Campeão da Série A x campeão da Copa" };

        var itens = new List<string>();

        if (e.TotalTimes > 0)
            itens.Add($"{e.TotalTimes} times");

        if (e.Grupos > 1)
            itens.Add(e.TotalTimes % e.Grupos == 0
                ? $"{e.Grupos} grupos de {e.TotalTimes / e.Grupos}"
                : $"{e.Grupos} grupos");

        if (e.Rodadas > 0)
            itens.Add(RodadasTexto(e));

        var mataMata = MataMataTexto(e.FasesMataMata);
        if (mataMata is not null)
            itens.Add(mataMata);

        itens.AddRange(AcessoTexto(e));

        return itens;
    }

    private static string RodadasTexto(LigaEdicaoDto e)
    {
        var rodadas = $"{e.Rodadas} {(e.Rodadas == 1 ? "rodada" : "rodadas")}";

        if (e.Grupos > 1)
            return $"{rodadas} na fase de grupos";

        if (e.TotalTimes > 1 && e.Rodadas >= (e.TotalTimes - 1) * 2)
            return $"{rodadas} (ida e volta)";

        if (e.TotalTimes > 1 && e.Rodadas >= e.TotalTimes - 1)
            return $"{rodadas} (turno único)";

        return rodadas;
    }

    private static string? MataMataTexto(IReadOnlyList<FaseKnockout> fases)
    {
        if (fases.Count == 0) return null;

        var partes = new List<string>();
        if (fases.Any(f => f is FaseKnockout.PlayIn_A or FaseKnockout.PlayIn_B or FaseKnockout.PlayIn_C))
            partes.Add("play-in");
        if (fases.Any(f => f is FaseKnockout.QF1 or FaseKnockout.QF2 or FaseKnockout.QF3 or FaseKnockout.QF4))
            partes.Add("quartas");
        if (fases.Any(f => f is FaseKnockout.Semi1 or FaseKnockout.Semi2))
            partes.Add("semifinais");
        if (fases.Contains(FaseKnockout.Final))
            partes.Add("final");

        return partes.Count == 0 ? null : $"Mata-mata: {Juntar(partes)}";
    }

    private static IEnumerable<string> AcessoTexto(LigaEdicaoDto e)
    {
        var regra = LigaRegraZonas.De(e.Tipo, e.Divisao, e.VagasDiretas, e.VagasPlayoff);
        if (!regra.TemAcessoRebaixamento) yield break;

        if (regra.Divisao == Divisao.SerieB)
        {
            if (regra.VagasDiretas > 0)
                yield return regra.VagasDiretas == 1 ? "1 sobe direto" : $"{regra.VagasDiretas} sobem direto";
            if (regra.VagasPlayoff > 0)
                yield return regra.VagasPlayoff == 1 ? "1 no playoff de acesso" : $"{regra.VagasPlayoff} no playoff de acesso";
            yield break;
        }

        if (regra.VagasDiretas > 0)
            yield return regra.VagasDiretas == 1 ? "1 rebaixado direto" : $"{regra.VagasDiretas} rebaixados direto";
        if (regra.VagasPlayoff > 0)
            yield return regra.VagasPlayoff == 1 ? "1 no playoff de rebaixamento" : $"{regra.VagasPlayoff} no playoff de rebaixamento";
    }

    private static string Juntar(IReadOnlyList<string> partes) =>
        partes.Count == 1
            ? partes[0]
            : $"{string.Join(", ", partes.Take(partes.Count - 1))} e {partes[^1]}";
}
