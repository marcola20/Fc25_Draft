namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Números da regra de participação: quanto o jogador joga mexe no valor de mercado dele, e quem tem nível para
/// jogar e fica no banco pede para sair.
/// </summary>
public static class ParticipacaoCriterios
{
    /// <summary>Últimos jogos do clube que contam (desde que o jogador chegou; jogo suspenso não entra).</summary>
    public const int JanelaDeJogos = 8;

    /// <summary>Com menos jogos do que isso no clube o valor não muda (quem acabou de chegar não é punido).</summary>
    public const int JogosParaValer = 3;

    public const decimal PesoTitular = 1m;
    public const decimal PesoEntrouDoBanco = 0.5m;

    /// <summary>Variação no valor pela participação: a partir de quanto (0 a 1), quanto muda.</summary>
    public static readonly IReadOnlyList<(decimal APartirDe, decimal Variacao)> Faixas =
    [
        (0.75m, +0.15m),
        (0.50m, +0.05m),
        (0.25m, 0m),
        (0.10m, -0.15m),
        (0m, -0.30m),
    ];

    /// <summary>Todo time precisa de um goleiro reserva: o goleiro nunca perde mais que isso.</summary>
    public const decimal PisoDoGoleiro = -0.10m;

    // Insatisfeito: tem nível para jogar e quase não joga.
    public const int JanelaDoInsatisfeito = 6;
    /// <summary>Jogou isso ou menos da janela: reclama (com a janela de 6, 25% = 1 titular e 1 entrada do banco).</summary>
    public const decimal InsatisfeitoAte = 0.25m;
    public const decimal DescontoDoInsatisfeito = -0.10m;
    /// <summary>Titular em tantos jogos seguidos: a insatisfação passa.</summary>
    public const int TitularSeguidoParaResolver = 2;
    /// <summary>"Tem nível": overall pelo menos a média dos 11 melhores do elenco menos isso.</summary>
    public const int MargemDoNivel = 1;
}

/// <summary>Um jogo do clube para o jogador, já sem os jogos em que ele estava suspenso.</summary>
public sealed record JogoDoJogador(Guid PartidaId, DateTime Data, bool Titular, bool EntrouDoBanco)
{
    public decimal Peso => Titular ? ParticipacaoCriterios.PesoTitular
        : EntrouDoBanco ? ParticipacaoCriterios.PesoEntrouDoBanco
        : 0m;
}

/// <summary>A conta do jogador logo depois de um jogo do clube (para o gráfico jogo a jogo).</summary>
public sealed record PontoDeParticipacao(JogoDoJogador Jogo, ParticipacaoJogador Depois);

/// <summary>Momento em que o jogador ficou insatisfeito (ou deixou de ficar), no jogo que decidiu.</summary>
public sealed record MudancaDeHumor(Guid PartidaId, DateTime Data, bool Insatisfeito, decimal PercentualNaJanela);

/// <summary>
/// Como o jogador está jogando no clube atual. <see cref="Percentual"/> é da janela (titular 1, banco ½);
/// <see cref="Variacao"/> é o quanto o valor de mercado muda (−0,40 a +0,15; mais baixo para quem pediu para sair e
/// teve a promessa quebrada).
/// </summary>
/// <param name="ChegouEmBaixa">Pediu para sair do clube anterior: o preço em baixa vale até a carência acabar.</param>
/// <param name="Travada">Pediu para sair e está na lista: a variação é a travada naquele momento.</param>
/// <param name="Saida">Caminho de saída (pediu para sair, promessa); nulo para quem não tem clube.</param>
/// <param name="Papel">O papel no elenco (nulo enquanto a regra do papel não está no ar).</param>
/// <param name="Incomodado">Não está insatisfeito, mas fica se não jogar o próximo jogo do clube.</param>
public sealed record ParticipacaoJogador(
    Guid TimeId,
    int Jogos,
    int Titular,
    int EntrouDoBanco,
    decimal? Percentual,
    decimal Variacao,
    bool Insatisfeito,
    IReadOnlyList<MudancaDeHumor> Humor,
    bool ChegouEmBaixa = false,
    bool Travada = false,
    SituacaoDeSaida? Saida = null,
    Expectativa? Papel = null,
    bool Incomodado = false)
{
    /// <summary>Ainda sem jogos suficientes no clube para o valor mudar.</summary>
    public bool EmCarencia => Jogos < ParticipacaoCriterios.JogosParaValer;

    /// <summary>Jogos que contam para ficar insatisfeito (prata da casa tem mais).</summary>
    public int JanelaDoInsatisfeito => Papel?.Janela ?? ParticipacaoCriterios.JanelaDoInsatisfeito;

    /// <summary>Ainda com o valor mais baixo pela conversa que rebaixou o papel.</summary>
    public bool NaConversa { get; init; }

    /// <summary>Quando ficou insatisfeito pela última vez (nulo se não está).</summary>
    public MudancaDeHumor? InsatisfeitoDesde => Insatisfeito ? Humor.LastOrDefault(h => h.Insatisfeito) : null;
}

public static class Participacao
{
    /// <summary>Variação no valor pela faixa de participação (0 a 1).</summary>
    public static decimal VariacaoDaFaixa(decimal percentual) =>
        ParticipacaoCriterios.Faixas.First(f => percentual >= f.APartirDe).Variacao;

    /// <summary>Participação (0 a 1) nos últimos <paramref name="janela"/> jogos até o índice <paramref name="ate"/>.</summary>
    private static decimal PercentualNaJanela(IReadOnlyList<JogoDoJogador> jogos, int ate, int janela)
    {
        var inicio = Math.Max(0, ate - janela + 1);
        var n = ate - inicio + 1;
        if (n <= 0) return 0m;
        decimal soma = 0;
        for (var i = inicio; i <= ate; i++) soma += jogos[i].Peso;
        return soma / n;
    }

    /// <summary>
    /// A conta de um jogador no clube atual.
    /// </summary>
    /// <param name="jogos">Jogos do clube desde que ele chegou, em ordem, sem os jogos em que estava suspenso.</param>
    /// <param name="goleiro">Goleiro tem piso de <see cref="ParticipacaoCriterios.PisoDoGoleiro"/>.</param>
    /// <param name="temNivel">Overall perto da média do XI do clube: só quem tem nível fica insatisfeito.</param>
    /// <param name="variacaoHerdada">
    /// Saiu do clube anterior depois de pedir para sair: o preço em baixa com que chegou vale enquanto ele está em
    /// carência (a satisfação volta, o preço não).
    /// </param>
    /// <param name="variacaoTravada">Pediu para sair e está na lista: o preço fica travado nessa variação.</param>
    /// <param name="papel">
    /// Papel no elenco: nos jogos a partir de <see cref="Expectativa.ValeDesde"/> ele decide quando o jogador reclama, e
    /// o valor não cai enquanto ele cumpre o papel. Nulo (ou jogos antes disso) = a regra antiga, pelo
    /// <paramref name="temNivel"/>.
    /// </param>
    public static ParticipacaoJogador Calcular(Guid timeId, IReadOnlyList<JogoDoJogador> jogos, bool goleiro, bool temNivel,
        decimal? variacaoHerdada = null, decimal? variacaoTravada = null, Expectativa? papel = null)
    {
        // Reclama no jogo i? Pela regra do papel nos jogos em que ela vale, pela antiga nos de antes.
        bool Reclama(IReadOnlyList<JogoDoJogador> lista, int i, out decimal percentual)
        {
            var regraDoPapel = papel is not null && lista[i].Data >= papel.ValeDesde;
            var janelaDoJogo = regraDoPapel ? papel!.Janela : ParticipacaoCriterios.JanelaDoInsatisfeito;
            percentual = PercentualNaJanela(lista, i, janelaDoJogo);
            if (i + 1 < janelaDoJogo) return false;
            return regraDoPapel
                ? papel!.Reclama(percentual)
                : temNivel && percentual <= ParticipacaoCriterios.InsatisfeitoAte;
        }

        // Humor jogo a jogo: entra quando a janela do insatisfeito fecha abaixo do limite; sai com titular seguido.
        var humor = new List<MudancaDeHumor>();
        var insatisfeito = false;
        var titularSeguido = 0;
        for (var i = 0; i < jogos.Count; i++)
        {
            var jogo = jogos[i];
            titularSeguido = jogo.Titular ? titularSeguido + 1 : 0;
            var reclama = Reclama(jogos, i, out var percentual);

            if (insatisfeito)
            {
                if (titularSeguido >= ParticipacaoCriterios.TitularSeguidoParaResolver)
                {
                    insatisfeito = false;
                    humor.Add(new MudancaDeHumor(jogo.PartidaId, jogo.Data, false, percentual));
                }
            }
            else if (reclama)
            {
                insatisfeito = true;
                humor.Add(new MudancaDeHumor(jogo.PartidaId, jogo.Data, true, percentual));
            }
        }

        // Incomodado: se não jogar o próximo, reclama (só com a regra do papel, que tem o aviso).
        var incomodado = false;
        if (papel is not null && !insatisfeito && jogos.Count > 0)
        {
            var comOProximo = jogos.Append(new JogoDoJogador(Guid.Empty, DateTime.MaxValue, false, false)).ToList();
            incomodado = Reclama(comOProximo, comOProximo.Count - 1, out _);
        }

        var janela = jogos.TakeLast(ParticipacaoCriterios.JanelaDeJogos).ToList();
        decimal? percentualAtual = janela.Count == 0 ? null : janela.Sum(j => j.Peso) / janela.Count;

        var variacao = 0m;
        var naConversa = papel?.ConversaEm is DateTime conversa
                         && jogos.Count(j => j.Data > conversa) < PapelCriterios.JogosDaConversa;
        if (jogos.Count >= ParticipacaoCriterios.JogosParaValer && percentualAtual is decimal p)
        {
            variacao = VariacaoDaFaixa(p);
            // Cumprindo o papel (o reserva que entra de vez em quando, a promessa), o valor não cai.
            if (papel is not null && p >= PapelCriterios.CumpreOPapel(papel.Papel)) variacao = Math.Max(variacao, 0m);
            if (insatisfeito) variacao += ParticipacaoCriterios.DescontoDoInsatisfeito;
            if (naConversa) variacao += PapelCriterios.DescontoDaConversa;
            if (goleiro) variacao = Math.Max(variacao, ParticipacaoCriterios.PisoDoGoleiro);
        }

        var chegouEmBaixa = jogos.Count < ParticipacaoCriterios.JogosParaValer && variacaoHerdada is not null;
        if (chegouEmBaixa) variacao = variacaoHerdada!.Value;
        if (variacaoTravada is decimal travada) variacao = travada;

        return new ParticipacaoJogador(
            timeId,
            janela.Count,
            janela.Count(j => j.Titular),
            janela.Count(j => !j.Titular && j.EntrouDoBanco),
            percentualAtual,
            variacao,
            insatisfeito,
            humor,
            chegouEmBaixa && variacaoTravada is null,
            variacaoTravada is not null,
            Papel: papel,
            Incomodado: incomodado)
        {
            NaConversa = naConversa
        };
    }

    /// <summary>A conta depois de cada jogo: o jogo i usa só os jogos até ele (e o papel só depois que ele valeu).</summary>
    public static IReadOnlyList<PontoDeParticipacao> Historico(
        Guid timeId, IReadOnlyList<JogoDoJogador> jogos, bool goleiro, bool temNivel, decimal? variacaoHerdada = null,
        Expectativa? papel = null) =>
        jogos.Select((jogo, i) => new PontoDeParticipacao(jogo,
                Calcular(timeId, jogos.Take(i + 1).ToList(), goleiro, temNivel, variacaoHerdada,
                    papel: papel is not null && jogo.Data >= papel.ValeDesde ? papel : null)))
            .ToList();

    /// <summary>"+15%", "−30%" (sinal de menos de verdade) ou "0%".</summary>
    public static string Rotulo(decimal variacao) =>
        variacao > 0 ? $"+{variacao * 100:0}%"
        : variacao < 0 ? $"−{-variacao * 100:0}%"
        : "0%";
}
