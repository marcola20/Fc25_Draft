using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

public enum ZonaClassificacao
{
    Nenhuma = 0,
    Campeao,
    CampeaoComAcesso,
    AcessoDireto,
    PlayoffAcesso,
    PlayoffRebaixamento,
    Rebaixamento
}

/// <summary>
/// Regras de zona de uma Liga, configuradas por liga (a quantidade muda de temporada para temporada:
/// ex. na transição de 12 para 10 times caem 2 direto e não há playoff).
/// </summary>
/// <param name="VagasDiretas">Série A: últimos que caem direto. Série B: primeiros que sobem direto.</param>
/// <param name="VagasPlayoff">Série A: quem vem logo acima dos rebaixados. Série B: quem vem logo abaixo dos promovidos.</param>
public sealed record LigaRegraZonas(Divisao? Divisao, int VagasDiretas, int VagasPlayoff)
{
    public static readonly LigaRegraZonas SoCampeao = new(null, 0, 0);

    public bool TemAcessoRebaixamento => Divisao is not null && VagasDiretas + VagasPlayoff > 0;

    public static LigaRegraZonas De(TipoCompetition tipo, Divisao? divisao, int? vagasDiretas, int? vagasPlayoff) =>
        tipo != TipoCompetition.Liga || divisao is null
            ? SoCampeao
            : new LigaRegraZonas(divisao, Math.Max(vagasDiretas ?? 0, 0), Math.Max(vagasPlayoff ?? 0, 0));

    public static LigaRegraZonas De(LigaDto liga) => De(liga.Tipo, liga.Divisao, liga.VagasDiretas, liga.VagasPlayoff);
}

/// <summary>
/// Zonas da classificação (Liga de pontos corridos). O 1º é sempre campeão.
/// <para><b>Série A:</b> os <c>VagasDiretas</c> últimos caem; os <c>VagasPlayoff</c> logo acima jogam o playoff.</para>
/// <para><b>Série B:</b> os <c>VagasDiretas</c> primeiros sobem; os <c>VagasPlayoff</c> logo abaixo jogam o playoff.</para>
/// </summary>
public static class LigaZonas
{
    public static ZonaClassificacao Zona(LigaRegraZonas regra, int posicao, int totalTimes)
    {
        if (regra.Divisao == Divisao.SerieB)
        {
            if (posicao <= regra.VagasDiretas)
                return posicao == 1 ? ZonaClassificacao.CampeaoComAcesso : ZonaClassificacao.AcessoDireto;
            if (posicao == 1)
                return ZonaClassificacao.Campeao;
            return posicao <= regra.VagasDiretas + regra.VagasPlayoff ? ZonaClassificacao.PlayoffAcesso : ZonaClassificacao.Nenhuma;
        }

        if (posicao == 1)
            return ZonaClassificacao.Campeao;

        if (regra.Divisao == Divisao.SerieA)
        {
            if (posicao > totalTimes - regra.VagasDiretas)
                return ZonaClassificacao.Rebaixamento;
            if (posicao > totalTimes - regra.VagasDiretas - regra.VagasPlayoff)
                return ZonaClassificacao.PlayoffRebaixamento;
        }

        return ZonaClassificacao.Nenhuma;
    }

    /// <summary>Zonas que aparecem na tabela, na ordem de cima para baixo (para a legenda).</summary>
    public static IReadOnlyList<ZonaClassificacao> ZonasDaTabela(LigaRegraZonas regra, int totalTimes) =>
        Enumerable.Range(1, Math.Max(totalTimes, 1))
            .Select(p => Zona(regra, p, totalTimes))
            .Where(z => z != ZonaClassificacao.Nenhuma)
            .Distinct()
            .ToList();

    /// <summary>
    /// Posições <c>p</c> em que um empate entre o <c>p</c>º e o <c>(p+1)</c>º muda a zona de alguém.
    /// A fronteira 1º/2º fica de fora: empate no topo é resolvido pela decisão de campeão.
    /// </summary>
    public static IReadOnlyList<int> Fronteiras(LigaRegraZonas regra, int totalTimes) =>
        Enumerable.Range(2, Math.Max(totalTimes - 2, 0))
            .Where(p => Zona(regra, p, totalTimes) != Zona(regra, p + 1, totalTimes))
            .ToList();

    public static string Rotulo(ZonaClassificacao zona) => zona switch
    {
        ZonaClassificacao.Campeao => "Campeão",
        ZonaClassificacao.CampeaoComAcesso => "Campeão e acesso à Série A",
        ZonaClassificacao.AcessoDireto => "Acesso à Série A",
        ZonaClassificacao.PlayoffAcesso => "Playoff de acesso",
        ZonaClassificacao.PlayoffRebaixamento => "Playoff contra o rebaixamento",
        ZonaClassificacao.Rebaixamento => "Rebaixado para a Série B",
        _ => ""
    };
}
