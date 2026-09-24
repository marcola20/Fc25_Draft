using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Xunit;

namespace Fc25Draft.Tests;

public class CarreiraJogadorTests
{
    private const int Jogador = 10;
    private const int Outro = 20;

    private static readonly Guid TimeA = Guid.NewGuid();
    private static readonly Guid TimeB = Guid.NewGuid();
    private static readonly Dictionary<Guid, string> Nomes = new() { [TimeA] = "Alfa", [TimeB] = "Beta" };

    private static readonly CarreiraLigaInput Antiga =
        new(Guid.NewGuid(), "Liga 2008", TipoCompetition.Liga, 2008, new DateTime(2025, 1, 1), TimeA, TemContagem: false);
    private static readonly CarreiraLigaInput Nova =
        new(Guid.NewGuid(), "Liga 2009", TipoCompetition.Liga, 2009, new DateTime(2026, 1, 1), TimeB, TemContagem: true);

    private static readonly Dictionary<Guid, CarreiraLigaInput> Ligas = new() { [Antiga.LigaId] = Antiga, [Nova.LigaId] = Nova };

    private static CarreiraEventoInput Gol(Guid liga, Guid time, Guid partida, int autor = Jogador, int? assistente = null) =>
        new(TipoEvento.Gol, autor, assistente, time, partida, liga);

    [Fact]
    public void Soma_por_competicao_e_time_com_titulos()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();

        var carreira = CarreiraJogador.Calcular(
            Jogador,
            defensor: false,
            Ligas,
            new[]
            {
                Gol(Antiga.LigaId, TimeA, Guid.NewGuid()),                        // competição sem contador
                Gol(Nova.LigaId, TimeB, p1),
                Gol(Nova.LigaId, TimeB, p1, autor: Outro, assistente: Jogador),   // assistência
                Gol(Nova.LigaId, TimeB, p2, autor: Outro),                        // não é dele
                new CarreiraEventoInput(TipoEvento.CartaoAmarelo, Jogador, null, TimeB, p3, Nova.LigaId)
            },
            new[]
            {
                new CarreiraParticipacaoInput(TimeB, p1, Nova.LigaId, false),
                new CarreiraParticipacaoInput(TimeB, p2, Nova.LigaId, true),
                new CarreiraParticipacaoInput(TimeB, p2, Nova.LigaId, true),     // duplicada (titular e substituição): 1 jogo
                new CarreiraParticipacaoInput(TimeB, p3, Nova.LigaId, false)
            },
            Nomes);

        Assert.Equal((2, 1, 1, 0), (carreira.Gols, carreira.Assistencias, carreira.Amarelos, carreira.Vermelhos));
        Assert.Equal(3, carreira.Jogos);
        Assert.True(carreira.JogosParcial);
        Assert.Null(carreira.CleanSheets); // não é defensor

        // Mais recente primeiro.
        Assert.Equal(new[] { "Liga 2009", "Liga 2008" }, carreira.Competicoes.Select(c => c.Competicao));
        var nova = carreira.Competicoes[0];
        Assert.Equal(("Beta", 3, 1, 1), (nova.TimeNome, nova.Jogos, nova.Gols, nova.Assistencias));
        Assert.Null(carreira.Competicoes[1].Jogos);

        // Campeão com o time em que jogou nas duas ligas.
        Assert.Equal(new[] { "Liga 2009", "Liga 2008" }, carreira.Titulos.Select(t => t.Competicao));
    }

    [Fact]
    public void Trocou_de_time_na_mesma_competicao_vira_duas_linhas()
    {
        var carreira = CarreiraJogador.Calcular(
            Jogador,
            defensor: false,
            Ligas,
            new[] { Gol(Nova.LigaId, TimeA, Guid.NewGuid()), Gol(Nova.LigaId, TimeB, Guid.NewGuid()) },
            Array.Empty<CarreiraParticipacaoInput>(),
            Nomes);

        Assert.Equal(2, carreira.Competicoes.Count);
        Assert.Single(carreira.Titulos); // só pelo Beta, o campeão
        Assert.Equal("Beta", carreira.Titulos[0].TimeNome);
    }

    [Fact]
    public void Defensor_conta_clean_sheets_so_onde_jogou_com_contador()
    {
        var carreira = CarreiraJogador.Calcular(
            Jogador,
            defensor: true,
            Ligas,
            Array.Empty<CarreiraEventoInput>(),
            new[]
            {
                new CarreiraParticipacaoInput(TimeB, Guid.NewGuid(), Nova.LigaId, true),
                new CarreiraParticipacaoInput(TimeB, Guid.NewGuid(), Nova.LigaId, true),
                new CarreiraParticipacaoInput(TimeB, Guid.NewGuid(), Nova.LigaId, false)
            },
            Nomes);

        Assert.Equal(2, carreira.CleanSheets);
        Assert.Equal(2, carreira.Competicoes.Single().CleanSheets);
    }

    [Fact]
    public void Hattricks_e_mais_gols_num_jogo()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var carreira = CarreiraJogador.Calcular(
            Jogador,
            defensor: false,
            Ligas,
            new[]
            {
                Gol(Nova.LigaId, TimeB, p1), Gol(Nova.LigaId, TimeB, p1), Gol(Nova.LigaId, TimeB, p1), Gol(Nova.LigaId, TimeB, p1),
                Gol(Nova.LigaId, TimeB, p2)
            },
            Array.Empty<CarreiraParticipacaoInput>(),
            Nomes);

        Assert.Equal(4, carreira.MaisGolsNumJogo);
        Assert.Equal(1, carreira.Hattricks);
    }

    [Fact]
    public void Sem_nada()
    {
        var carreira = CarreiraJogador.Calcular(
            Jogador, defensor: true, Ligas,
            Array.Empty<CarreiraEventoInput>(), Array.Empty<CarreiraParticipacaoInput>(), Nomes);

        Assert.Empty(carreira.Competicoes);
        Assert.Equal(0, carreira.Jogos);
        Assert.False(carreira.JogosParcial);
        Assert.Equal(0, carreira.MaisGolsNumJogo);
        Assert.Equal(0, carreira.CleanSheets);
    }
}
