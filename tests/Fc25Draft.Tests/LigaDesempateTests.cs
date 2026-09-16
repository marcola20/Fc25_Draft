using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Xunit;

namespace Fc25Draft.Tests;

public class LigaDesempateTests
{
    private sealed record Time(string Nome, Guid TimeId, DesempateStats Stats);

    private static Time Novo(string nome, int pontos, int vitorias, int saldo, int golsPro) =>
        new(nome, Guid.NewGuid(), new DesempateStats(pontos, vitorias, saldo, golsPro));

    private static List<Time> Ordenar(TipoCompetition tipo, IEnumerable<Time> times, params ConfrontoDireto[] confrontos) =>
        LigaDesempate.Ordenar(times, tipo, t => t.TimeId, t => t.Stats, confrontos);

    private static List<Time> OrdenarCopa(IEnumerable<Time> times, JogosDecisivos decisivos) =>
        LigaDesempate.Ordenar(times, TipoCompetition.Copa, t => t.TimeId, t => t.Stats,
            Array.Empty<ConfrontoDireto>(), decisivos);

    private static List<int> Posicoes(IReadOnlyList<Time> ordenados, JogosDecisivos? decisivos = null) =>
        LigaDesempate.PosicoesCopa(ordenados, t => t.TimeId, t => t.Stats, decisivos);

    [Fact]
    public void Copa_ordena_por_pontos_vitorias_e_saldo()
    {
        var maisVitorias = Novo("Vitórias", 10, 3, 5, 12);
        var maisSaldo = Novo("Saldo", 10, 2, 8, 12);
        var lider = Novo("Líder", 12, 4, 1, 9);

        var ordenados = Ordenar(TipoCompetition.Copa, new[] { maisSaldo, maisVitorias, lider });

        Assert.Equal(new[] { "Líder", "Vitórias", "Saldo" }, ordenados.Select(t => t.Nome));
    }

    [Fact]
    public void Copa_ignora_gols_pro_e_confronto_direto_e_mantem_times_empatados()
    {
        var a = Novo("A", 10, 3, 4, 20);
        var b = Novo("B", 10, 3, 4, 5);

        // Mesmo com A goleando B no confronto direto, eles seguem empatados na Copa.
        var ordenados = Ordenar(TipoCompetition.Copa, new[] { a, b },
            new ConfrontoDireto(a.TimeId, b.TimeId, 4, 0));

        Assert.Equal(new[] { 1, 1 }, Posicoes(ordenados));
        Assert.True(LigaDesempate.EmpatadosNaCopa(a.Stats, b.Stats));
    }

    [Fact]
    public void PosicoesCopa_retoma_a_numeracao_apos_o_empate()
    {
        var primeiro = Novo("1º", 12, 4, 6, 14);
        var empatadoA = Novo("2º A", 10, 3, 3, 11);
        var empatadoB = Novo("2º B", 10, 3, 3, 8);
        var ultimo = Novo("4º", 4, 1, -5, 6);

        var ordenados = Ordenar(TipoCompetition.Copa, new[] { ultimo, empatadoB, primeiro, empatadoA });

        Assert.Equal(new[] { 1, 2, 2, 4 }, Posicoes(ordenados));
    }

    [Fact]
    public void Jogo_decisivo_coloca_o_vencedor_na_frente_e_desfaz_o_empate()
    {
        var venceu = Novo("Venceu", 10, 3, 4, 12);
        var perdeu = Novo("Perdeu", 10, 3, 4, 12);
        var decisivos = new JogosDecisivos(new[] { (venceu.TimeId, perdeu.TimeId) });

        var ordenados = OrdenarCopa(new[] { perdeu, venceu }, decisivos);

        Assert.Equal(new[] { "Venceu", "Perdeu" }, ordenados.Select(t => t.Nome));
        Assert.Equal(new[] { 1, 2 }, Posicoes(ordenados, decisivos));
    }

    [Fact]
    public void Jogo_decisivo_nao_muda_quem_nao_estava_empatado()
    {
        var lider = Novo("Líder", 12, 4, 6, 14);
        var segundo = Novo("2º", 10, 3, 3, 11);
        var terceiro = Novo("3º", 10, 3, 3, 9);
        var decisivos = new JogosDecisivos(new[] { (terceiro.TimeId, segundo.TimeId) });

        var ordenados = OrdenarCopa(new[] { lider, segundo, terceiro }, decisivos);

        // O vencedor do decisivo assume o 2º lugar; o líder segue isolado.
        Assert.Equal(new[] { "Líder", "3º", "2º" }, ordenados.Select(t => t.Nome));
        Assert.Equal(new[] { 1, 2, 3 }, Posicoes(ordenados, decisivos));
    }

    [Fact]
    public void VencedorDoJogoDecisivo_usa_placar_e_penaltis()
    {
        var casa = Guid.NewGuid();
        var fora = Guid.NewGuid();

        Assert.Equal(casa, LigaDesempate.VencedorDoJogoDecisivo(casa, fora, 2, 1, false, null));
        Assert.Equal(fora, LigaDesempate.VencedorDoJogoDecisivo(casa, fora, 0, 3, false, null));
        Assert.Equal(fora, LigaDesempate.VencedorDoJogoDecisivo(casa, fora, 1, 1, true, fora));
        Assert.Null(LigaDesempate.VencedorDoJogoDecisivo(casa, fora, 1, 1, false, null));
    }

    [Fact]
    public void Liga_desempata_por_gols_pro_antes_do_confronto_direto()
    {
        var maisGols = Novo("Mais gols", 10, 3, 4, 20);
        var menosGols = Novo("Menos gols", 10, 3, 4, 5);

        var ordenados = Ordenar(TipoCompetition.Liga, new[] { menosGols, maisGols },
            new ConfrontoDireto(menosGols.TimeId, maisGols.TimeId, 1, 0));

        Assert.Equal(new[] { "Mais gols", "Menos gols" }, ordenados.Select(t => t.Nome));
    }

    [Fact]
    public void Liga_usa_confronto_direto_quando_todos_os_numeros_empatam()
    {
        var venceuOConfronto = Novo("Venceu", 10, 3, 4, 12);
        var perdeuOConfronto = Novo("Perdeu", 10, 3, 4, 12);

        var ordenados = Ordenar(TipoCompetition.Liga, new[] { perdeuOConfronto, venceuOConfronto },
            new ConfrontoDireto(perdeuOConfronto.TimeId, venceuOConfronto.TimeId, 0, 2));

        Assert.Equal(new[] { "Venceu", "Perdeu" }, ordenados.Select(t => t.Nome));
    }
}
