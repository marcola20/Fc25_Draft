namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Números do "pediu para sair": o insatisfeito que segue no banco vai sozinho para a lista de transferências pelo
/// preço da venda rápida, e o dono só tira com a promessa de colocar para jogar.
/// </summary>
public static class PedidoDeSaidaCriterios
{
    /// <summary>Jogos do clube depois de ficar insatisfeito (ainda insatisfeito) até pedir para sair.</summary>
    public const int JogosAtePedir = 3;

    /// <summary>Jogos que a promessa tem para fazer as pazes (titular 2 jogos seguidos).</summary>
    public const int JogosDaPromessa = 5;

    /// <summary>Promessas em andamento ao mesmo tempo, por time.</summary>
    public const int PromessasPorTime = 1;

    /// <summary>Promessa quebrada: volta à lista por essa fração do preço da venda rápida (e a venda rápida também).</summary>
    public const decimal FatorDaPromessaQuebrada = 0.5m;

    /// <summary>A venda rápida paga essa fração do valor de mercado: é o preço de quem pede para sair.</summary>
    public const decimal FatorDaVendaRapida = 0.8m;
}

/// <summary>
/// Onde o jogador está no caminho de saída do clube atual.
/// </summary>
/// <param name="SaidaPedidaEm">Quando pediu para sair: está na lista com o preço travado.</param>
/// <param name="JogosInsatisfeito">
/// Jogos do clube que já contam para ele pedir para sair (insatisfeito, nem goleiro nem emprestado); nulo se não conta.
/// </param>
/// <param name="PromessaEm">Promessa de colocar para jogar em andamento desde.</param>
/// <param name="JogosDaPromessa">Jogos do clube desde a promessa.</param>
/// <param name="PromessaQuebrada">Já quebraram uma promessa com ele: está na lista pela metade e não aceita outra.</param>
public sealed record SituacaoDeSaida(
    DateTime? SaidaPedidaEm,
    int? JogosInsatisfeito,
    DateTime? PromessaEm,
    int JogosDaPromessa,
    bool PromessaQuebrada)
{
    public bool NaLista => SaidaPedidaEm is not null;

    /// <summary>Jogos que faltam para ele pedir para sair (nulo se não está contando).</summary>
    public int? FaltamParaPedir => JogosInsatisfeito is int n && !NaLista && PromessaEm is null
        ? Math.Max(0, PedidoDeSaidaCriterios.JogosAtePedir - n)
        : null;

    /// <summary>Jogos que a promessa ainda tem.</summary>
    public int FaltamDaPromessa => Math.Max(0, PedidoDeSaidaCriterios.JogosDaPromessa - JogosDaPromessa);
}

public static class PedidoDeSaida
{
    /// <summary>
    /// Jogos depois do que deixou o jogador insatisfeito, só os encerrados depois de <paramref name="contaDesde"/>
    /// (quando a regra entrou no ar ou a virada recomeçou a contagem).
    /// </summary>
    public static int JogosInsatisfeito(IReadOnlyList<JogoDoJogador> jogos, MudancaDeHumor desde, DateTime contaDesde)
    {
        var i = -1;
        for (var k = 0; k < jogos.Count; k++)
            if (jogos[k].PartidaId == desde.PartidaId) i = k;
        return jogos.Skip(i + 1).Count(j => j.Data > contaDesde);
    }

    /// <summary>Jogos do clube encerrados depois de <paramref name="desde"/>.</summary>
    public static int JogosDepois(IReadOnlyList<JogoDoJogador> jogos, DateTime desde) =>
        jogos.Count(j => j.Data > desde);

    /// <summary>Variação com a promessa quebrada: o valor (e a venda rápida) cai pela metade.</summary>
    public static decimal VariacaoDaPromessaQuebrada(decimal variacao) =>
        decimal.Round((1 + variacao) * PedidoDeSaidaCriterios.FatorDaPromessaQuebrada - 1, 4, MidpointRounding.AwayFromZero);

    /// <summary>Preço na lista: o que a venda rápida pagaria pelo valor de mercado de agora.</summary>
    public static decimal PrecoNaLista(decimal valorDeMercado) =>
        decimal.Round(valorDeMercado * PedidoDeSaidaCriterios.FatorDaVendaRapida, 2, MidpointRounding.AwayFromZero);
}
