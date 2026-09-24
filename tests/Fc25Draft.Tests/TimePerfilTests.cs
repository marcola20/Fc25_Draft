using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Xunit;

namespace Fc25Draft.Tests;

public class TimePerfilTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static readonly Dictionary<Guid, string> Nomes = new() { [A] = "Alfa", [B] = "Beta", [C] = "Gama" };

    private static int _ordem;

    /// <summary>Jogo encerrado; cada chamada acontece depois da anterior.</summary>
    private static TimePerfilPartidaInput Jogo(Guid casa, Guid fora, int golsCasa, int golsFora, Guid? venceuPenaltis = null)
    {
        _ordem++;
        return new TimePerfilPartidaInput(
            Guid.NewGuid(), "Série A", $"Rodada {_ordem}", casa, fora, golsCasa, golsFora,
            PartidaStatus.Encerrada, false, venceuPenaltis is not null, venceuPenaltis,
            new DateTime(2026, 1, 1).AddDays(_ordem), _ordem);
    }

    private static TimePerfilPartidaInput Agendado(Guid casa, Guid fora, long ordem) =>
        new(Guid.NewGuid(), "Copa", $"Rodada {ordem}", casa, fora, 0, 0,
            PartidaStatus.Agendada, false, false, null, null, ordem);

    [Fact]
    public void Soma_retrospecto_forma_e_confrontos()
    {
        var perfil = TimePerfil.Calcular(A, new[]
        {
            Jogo(A, B, 3, 0),
            Jogo(C, A, 1, 1),
            Jogo(B, A, 2, 1),
            Jogo(A, C, 0, 0, venceuPenaltis: A),
            Jogo(B, C, 5, 0) // não é do time: ignorado
        }, Nomes);

        Assert.Equal((4, 1, 2, 1), (perfil.Jogos, perfil.Vitorias, perfil.Empates, perfil.Derrotas));
        Assert.Equal((5, 3), (perfil.GolsPro, perfil.GolsContra));

        // Forma do mais recente para o mais antigo; pênaltis contam como empate.
        Assert.Equal(new[] { "E", "D", "E", "V" }, perfil.Forma.Select(j => j.Resultado));
        Assert.True(perfil.Forma[0].VenceuPenaltis);

        var beta = perfil.Confrontos.Single(c => c.AdversarioId == B);
        Assert.Equal((2, 1, 0, 1, 4, 2), (beta.Jogos, beta.Vitorias, beta.Empates, beta.Derrotas, beta.GolsPro, beta.GolsContra));
        Assert.Equal("Beta", beta.AdversarioNome);
    }

    [Fact]
    public void Recordes_e_maiores_sequencias()
    {
        var perfil = TimePerfil.Calcular(A, new[]
        {
            Jogo(A, B, 1, 0),
            Jogo(A, C, 2, 0),
            Jogo(B, A, 0, 0),
            Jogo(C, A, 4, 1),
            Jogo(A, B, 4, 2),
            Jogo(A, C, 3, 1),
        }, Nomes);

        Assert.Equal(2, perfil.MaiorSequenciaVitorias);
        Assert.Equal(3, perfil.MaiorInvencibilidade);

        // Empate no saldo (+2): vale quem marcou mais gols.
        Assert.Equal((4, 2), (perfil.MaiorVitoria!.GolsPro, perfil.MaiorVitoria.GolsContra));
        Assert.Equal((1, 4), (perfil.MaiorDerrota!.GolsPro, perfil.MaiorDerrota.GolsContra));
    }

    [Theory]
    [InlineData(new[] { "V", "V", "V" }, "3 vitórias seguidas", "V")]
    [InlineData(new[] { "D", "E", "V", "V" }, "Invicto há 3 jogos", "V")]
    [InlineData(new[] { "V", "E" }, "Invicto há 2 jogos", "E")]
    [InlineData(new[] { "D", "V" }, "Venceu o último jogo", "V")]
    [InlineData(new[] { "D", "D" }, "2 derrotas seguidas", "D")]
    [InlineData(new[] { "V", "E", "D" }, "Sem vencer há 2 jogos", "D")]
    [InlineData(new[] { "V", "D" }, "Perdeu o último jogo", "D")]
    public void Sequencia_atual(string[] resultadosEmOrdem, string texto, string tipo)
    {
        var jogos = resultadosEmOrdem.Select(r => r switch
        {
            "V" => Jogo(A, B, 1, 0),
            "D" => Jogo(A, B, 0, 1),
            _ => Jogo(A, B, 1, 1)
        });

        var perfil = TimePerfil.Calcular(A, jogos, Nomes);

        Assert.Equal(texto, perfil.SequenciaAtual);
        Assert.Equal(tipo, perfil.SequenciaTipo);
    }

    [Fact]
    public void Proximos_jogos_na_ordem_da_agenda()
    {
        var perfil = TimePerfil.Calcular(A, new[]
        {
            Agendado(A, C, 20),
            Agendado(B, A, 10),
            Jogo(A, B, 1, 0)
        }, Nomes);

        Assert.Equal(new[] { "Beta", "Gama" }, perfil.ProximosJogos.Select(j => j.AdversarioNome));
        Assert.False(perfil.ProximoJogo!.EmCasa);
        Assert.Null(perfil.ProximoJogo.Resultado);
        Assert.Equal(1, perfil.Jogos);
    }

    [Fact]
    public void Sem_jogos()
    {
        var perfil = TimePerfil.Calcular(A, Array.Empty<TimePerfilPartidaInput>(), Nomes);

        Assert.Equal(0, perfil.Jogos);
        Assert.Null(perfil.SequenciaAtual);
        Assert.Null(perfil.MaiorVitoria);
        Assert.Null(perfil.ProximoJogo);
        Assert.Empty(perfil.Forma);
    }
}
