using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Xunit;

namespace Fc25Draft.Tests;

public class RankingClubesTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static readonly Dictionary<Guid, string> Nomes = new() { [A] = "Alfa", [B] = "Beta", [C] = "Gama" };

    [Fact]
    public void Soma_jogos_titulos_fases_e_posicao()
    {
        var ranking = RankingClubes.Calcular(
            Nomes,
            new[]
            {
                new RankingPartidaInput(A, B, 2, 0),
                new RankingPartidaInput(B, C, 1, 1),
                new RankingPartidaInput(C, A, 0, 1)
            },
            new[] { new RankingTituloInput(A, TipoCompetition.Copa, null) },
            new[] { new RankingFaseInput(A, true), new RankingFaseInput(B, true), new RankingFaseInput(C, false) },
            new[]
            {
                new RankingPosicaoInput(A, Divisao.SerieA, 1, 3),
                new RankingPosicaoInput(B, Divisao.SerieA, 2, 3),
                new RankingPosicaoInput(C, Divisao.SerieB, 1, 3)
            });

        var alfa = ranking.Single(r => r.TimeId == A);
        Assert.Equal(1, alfa.Posicao);
        Assert.Equal((2, 0, 0), (alfa.Vitorias, alfa.Empates, alfa.Derrotas));
        Assert.Equal(1, alfa.TitulosCopa);
        // 6 (jogos) + 15 (Copa) + 6 (final) + 6 (1º de 3 na A)
        Assert.Equal(33, alfa.Pontos);

        var beta = ranking.Single(r => r.TimeId == B);
        // 1 (empate) + 6 (final) + 4 (2º de 3 na A)
        Assert.Equal(11, beta.Pontos);

        var gama = ranking.Single(r => r.TimeId == C);
        // 1 (empate) + 3 (semi) + 3 (1º de 3 na B)
        Assert.Equal(7, gama.Pontos);
        Assert.Null(gama.MelhorPosicaoSerieA);
    }

    [Fact]
    public void Empate_em_pontos_desempata_por_titulos()
    {
        var ranking = RankingClubes.Calcular(
            Nomes,
            Enumerable.Repeat(new RankingPartidaInput(A, B, 1, 0), 3), // Alfa 9 pts de jogos
            new[] { new RankingTituloInput(B, TipoCompetition.Supercopa, null) }, // Beta 8 + ...
            new[] { new RankingFaseInput(B, false) }, // ... + 3 = 11
            new[] { new RankingPosicaoInput(A, Divisao.SerieA, 2, 2) }); // Alfa 9 + 2 = 11

        Assert.Equal(new[] { B, A }, ranking.Select(r => r.TimeId));
    }

    [Fact]
    public void Liga_sem_divisao_vale_como_serie_a()
    {
        Assert.Equal(RankingClubesCriterios.TituloSerieA, RankingClubesCriterios.Titulo(TipoCompetition.Liga, null));
        Assert.Equal(24, RankingClubesCriterios.Posicao(null, 1, 12));
    }
}
