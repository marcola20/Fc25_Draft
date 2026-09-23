using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Pontuação do Ranking de Clubes. Os pesos ficam aqui para a página mostrar
/// exatamente os mesmos critérios usados no cálculo.
/// </summary>
public static class RankingClubesCriterios
{
    public const int Vitoria = 3;
    public const int Empate = 1;

    public const int TituloSerieA = 25;
    public const int TituloCopa = 15;
    public const int TituloSerieB = 10;
    public const int TituloSupercopa = 8;

    /// <summary>Por final disputada (Copa, mata-mata da liga ou Supercopa), ganhando ou perdendo.</summary>
    public const int Final = 6;

    /// <summary>Por semifinal disputada.</summary>
    public const int Semifinal = 3;

    /// <summary>
    /// Posição final na liga: na Série A vale 2 pontos por time deixado para trás (+2 do próprio),
    /// na Série B vale 1. Ex.: 1º de 10 na A = 20; 10º da A = 2; 1º de 8 na B = 8.
    /// </summary>
    public const int PosicaoSerieA = 2;
    public const int PosicaoSerieB = 1;

    public static int Titulo(TipoCompetition tipo, Divisao? divisao) => tipo switch
    {
        TipoCompetition.Copa => TituloCopa,
        TipoCompetition.Supercopa => TituloSupercopa,
        _ => divisao == Divisao.SerieB ? TituloSerieB : TituloSerieA
    };

    public static int Posicao(Divisao? divisao, int posicao, int totalTimes) =>
        Math.Max(0, totalTimes - posicao + 1) * (divisao == Divisao.SerieB ? PosicaoSerieB : PosicaoSerieA);
}

public record RankingPartidaInput(Guid CasaId, Guid ForaId, int GolsCasa, int GolsFora);
public record RankingTituloInput(Guid TimeId, TipoCompetition Tipo, Divisao? Divisao);
public record RankingFaseInput(Guid TimeId, bool Final);
public record RankingPosicaoInput(Guid TimeId, Divisao? Divisao, int Posicao, int TotalTimes);

public static class RankingClubes
{
    /// <summary>
    /// Soma tudo por time e ordena: pontos, títulos, vitórias, saldo e nome.
    /// Jogo decidido nos pênaltis conta como empate (o placar é o do tempo normal).
    /// </summary>
    public static IReadOnlyList<RankingClubeDto> Calcular(
        IReadOnlyDictionary<Guid, string> nomes,
        IEnumerable<RankingPartidaInput> partidas,
        IEnumerable<RankingTituloInput> titulos,
        IEnumerable<RankingFaseInput> fases,
        IEnumerable<RankingPosicaoInput> posicoes)
    {
        var acc = new Dictionary<Guid, Acumulado>();
        Acumulado De(Guid id) => acc.TryGetValue(id, out var a) ? a : acc[id] = new Acumulado();

        foreach (var p in partidas)
        {
            Registrar(De(p.CasaId), p.GolsCasa, p.GolsFora);
            Registrar(De(p.ForaId), p.GolsFora, p.GolsCasa);
        }

        foreach (var t in titulos)
        {
            var a = De(t.TimeId);
            a.PontosTitulos += RankingClubesCriterios.Titulo(t.Tipo, t.Divisao);
            switch (t.Tipo)
            {
                case TipoCompetition.Copa: a.TitulosCopa++; break;
                case TipoCompetition.Supercopa: a.TitulosSupercopa++; break;
                default:
                    if (t.Divisao == Divisao.SerieB) a.TitulosSerieB++; else a.TitulosSerieA++;
                    break;
            }
        }

        foreach (var f in fases)
        {
            var a = De(f.TimeId);
            if (f.Final) a.Finais++; else a.Semifinais++;
        }

        foreach (var p in posicoes)
        {
            var a = De(p.TimeId);
            a.PontosPosicao += RankingClubesCriterios.Posicao(p.Divisao, p.Posicao, p.TotalTimes);
            if (p.Divisao != Divisao.SerieB && (a.MelhorPosicaoSerieA is null || p.Posicao < a.MelhorPosicaoSerieA))
                a.MelhorPosicaoSerieA = p.Posicao;
        }

        var ordenados = acc
            .Select(kv => (Id: kv.Key, Nome: nomes.GetValueOrDefault(kv.Key, "?"), A: kv.Value))
            .OrderByDescending(x => x.A.Pontos)
            .ThenByDescending(x => x.A.Titulos)
            .ThenByDescending(x => x.A.Vitorias)
            .ThenByDescending(x => x.A.GolsPro - x.A.GolsContra)
            .ThenBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return ordenados.Select((x, i) => new RankingClubeDto(
            i + 1, x.Id, x.Nome, x.A.Pontos,
            x.A.Jogos, x.A.Vitorias, x.A.Empates, x.A.Derrotas, x.A.GolsPro, x.A.GolsContra,
            x.A.TitulosSerieA, x.A.TitulosSerieB, x.A.TitulosCopa, x.A.TitulosSupercopa,
            x.A.Finais, x.A.Semifinais, x.A.MelhorPosicaoSerieA,
            x.A.PontosJogos, x.A.PontosTitulos, x.A.PontosFases, x.A.PontosPosicao)).ToArray();
    }

    private static void Registrar(Acumulado a, int pro, int contra)
    {
        a.Jogos++;
        a.GolsPro += pro;
        a.GolsContra += contra;
        if (pro > contra) a.Vitorias++;
        else if (pro == contra) a.Empates++;
        else a.Derrotas++;
    }

    private sealed class Acumulado
    {
        public int Jogos, Vitorias, Empates, Derrotas, GolsPro, GolsContra;
        public int TitulosSerieA, TitulosSerieB, TitulosCopa, TitulosSupercopa;
        public int Finais, Semifinais, PontosTitulos, PontosPosicao;
        public int? MelhorPosicaoSerieA;

        public int Titulos => TitulosSerieA + TitulosSerieB + TitulosCopa + TitulosSupercopa;
        public int PontosJogos => Vitorias * RankingClubesCriterios.Vitoria + Empates * RankingClubesCriterios.Empate;
        public int PontosFases => Finais * RankingClubesCriterios.Final + Semifinais * RankingClubesCriterios.Semifinal;
        public int Pontos => PontosJogos + PontosTitulos + PontosFases + PontosPosicao;
    }
}
