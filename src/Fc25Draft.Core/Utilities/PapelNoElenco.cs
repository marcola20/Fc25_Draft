using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// O que o jogador espera do clube. Define quanto ele precisa jogar para não ficar insatisfeito e quando o valor de
/// mercado para de cair. Vem de como ele chegou (leilão, draft, troca) e é refeito na virada pelo que ele jogou.
/// </summary>
public enum PapelNoElenco : short
{
    Titular = 1,
    Disputa = 2,
    Rotacao = 3,
    Promessa = 4
}

/// <summary>Grupo de posições que disputam a mesma vaga no time.</summary>
public enum GrupoDePosicao
{
    Goleiro,
    Zaga,
    Lateral,
    Meio,
    Ataque
}

/// <summary>Números do papel no elenco.</summary>
public static class PapelCriterios
{
    /// <summary>
    /// Quanto ele joga (0 a 1, na janela do insatisfeito) e ainda reclama. Titular reclama abaixo de 50% (não
    /// inclusivo); os outros reclamam com isso ou menos. Promessa não reclama.
    /// </summary>
    public static (decimal Limite, bool Inclusivo)? Reclama(PapelNoElenco papel) => papel switch
    {
        PapelNoElenco.Titular => (0.50m, false),
        PapelNoElenco.Disputa => (0.25m, true),
        PapelNoElenco.Rotacao => (0.10m, true),
        _ => null
    };

    /// <summary>Jogando isso ou mais (janela do valor), o valor de mercado não cai: ele está cumprindo o papel.</summary>
    public static decimal CumpreOPapel(PapelNoElenco papel) => papel switch
    {
        PapelNoElenco.Titular => 0.50m,
        PapelNoElenco.Disputa => 0.25m,
        PapelNoElenco.Rotacao => 0.10m,
        _ => 0m
    };

    // Leilão: o que pagou sobre o lance inicial.
    /// <summary>Pagou mais que isso (sobre o lance inicial): chega para disputar posição.</summary>
    public const decimal LeilaoDisputaAcimaDe = 1.25m;
    /// <summary>Pagou isso ou mais (ou usou o comprar já): chega como titular.</summary>
    public const decimal LeilaoTitularAPartirDe = 1.75m;

    // Virada: o que jogou na temporada que acabou.
    public const decimal ViradaTitularAPartirDe = 0.60m;
    public const decimal ViradaDisputaAPartirDe = 0.25m;
    /// <summary>Veterano: no máximo disputa, aceita o banco.</summary>
    public const int IdadeDoVeterano = 33;
    /// <summary>Jovem que não foi titular volta a ser promessa.</summary>
    public const int IdadeDaPromessa = 21;
    /// <summary>Do draft (2ª rodada em diante) chega como promessa quem tem até essa idade (a do draft de jovens).</summary>
    public const int IdadeDaPromessaNoDraft = 23;

    /// <summary>Temporadas completas no clube para virar prata da casa (mais paciência).</summary>
    public const int TemporadasDaPrataDaCasa = 2;
    /// <summary>Janela do insatisfeito para a prata da casa.</summary>
    public const int JanelaDaPrataDaCasa = 8;
    /// <summary>Chegou até tantos dias depois do início da temporada: conta como temporada inteira (o draft é no começo).</summary>
    public const int ToleranciaDoInicioEmDias = 7;

    /// <summary>Conversas para rebaixar o papel, por time e por temporada.</summary>
    public const int ConversasPorTemporada = 1;
    /// <summary>Depois da conversa o valor cai isso por tantos jogos do clube.</summary>
    public const decimal DescontoDaConversa = -0.05m;
    public const int JogosDaConversa = 5;

    /// <summary>Titulares por grupo quando o time ainda não tem escalação gravada.</summary>
    public static readonly IReadOnlyDictionary<GrupoDePosicao, int> TitularesPadrao = new Dictionary<GrupoDePosicao, int>
    {
        [GrupoDePosicao.Goleiro] = 1,
        [GrupoDePosicao.Zaga] = 2,
        [GrupoDePosicao.Lateral] = 2,
        [GrupoDePosicao.Meio] = 3,
        [GrupoDePosicao.Ataque] = 3,
    };

    /// <summary>Escalações recentes que contam para saber quantos titulares cada grupo tem.</summary>
    public const int JogosParaContarTitulares = 8;
}

/// <summary>Como o jogador chegou ao clube atual.</summary>
/// <param name="Leilao">Pago ÷ lance inicial (leilão de livres); nulo se não veio do leilão.</param>
/// <param name="CompraImediata">Leilão pelo comprar já.</param>
/// <param name="DraftRodada">Rodada do draft (normal ou de expansão); nulo se não veio do draft.</param>
/// <param name="DraftPick">Escolha dentro da rodada.</param>
/// <param name="DraftTimes">Times no draft (a primeira metade da 1ª rodada chega como titular).</param>
public sealed record ChegadaAoClube(
    decimal? Leilao = null,
    bool CompraImediata = false,
    int? DraftRodada = null,
    int? DraftPick = null,
    int? DraftTimes = null,
    bool Expansao = false);

/// <summary>O papel valendo para a conta do humor e do valor.</summary>
/// <param name="TemNivel">Está entre os melhores da posição dele no elenco (titular cobra mesmo sem isso).</param>
/// <param name="Janela">Jogos que contam para ficar insatisfeito (prata da casa tem mais paciência).</param>
/// <param name="ValeDesde">A regra do papel vale para os jogos a partir daqui (antes, a regra antiga).</param>
/// <param name="ConversaEm">O clube rebaixou o papel numa conversa (o valor cai um pouco por alguns jogos).</param>
public sealed record Expectativa(
    PapelNoElenco Papel,
    bool TemNivel,
    int Janela,
    DateTime ValeDesde,
    DateTime? ConversaEm = null,
    bool PrataDaCasa = false)
{
    /// <summary>Com <paramref name="percentual"/> na janela, ele reclama?</summary>
    public bool Reclama(decimal percentual)
    {
        if (PapelCriterios.Reclama(Papel) is not { } regra) return false;
        var (limite, inclusivo) = regra;
        if (Papel != PapelNoElenco.Titular && !TemNivel) return false;
        return inclusivo ? percentual <= limite : percentual < limite;
    }
}

public static class PapelDoJogador
{
    public static string Emoji(PapelNoElenco papel) => papel switch
    {
        PapelNoElenco.Titular => "⭐",
        PapelNoElenco.Disputa => "🔁",
        PapelNoElenco.Rotacao => "🪑",
        _ => "🌱"
    };

    public static string Nome(PapelNoElenco papel) => papel switch
    {
        PapelNoElenco.Titular => "Titular",
        PapelNoElenco.Disputa => "Disputa",
        PapelNoElenco.Rotacao => "Rotação",
        _ => "Promessa"
    };

    /// <summary>O que ele cobra, em uma frase.</summary>
    public static string OQueEspera(PapelNoElenco papel, int janela) => papel switch
    {
        PapelNoElenco.Titular => $"chegou para jogar: reclama se jogar menos de 50% dos últimos {janela} jogos",
        PapelNoElenco.Disputa => $"disputa posição: reclama com 25% ou menos dos últimos {janela} jogos, se for dos melhores da posição",
        PapelNoElenco.Rotacao => $"sabe que é reserva: só reclama sem nenhuma chance nos últimos {janela} jogos, se for dos melhores da posição",
        _ => "está aprendendo: não reclama nesta temporada e o valor não cai"
    };

    public static GrupoDePosicao Grupo(short positionId) => (PositionType)positionId switch
    {
        PositionType.Goleiro => GrupoDePosicao.Goleiro,
        PositionType.Zagueiro => GrupoDePosicao.Zaga,
        PositionType.LateralEsquerdo or PositionType.LateralDireito => GrupoDePosicao.Lateral,
        PositionType.PontaEsquerda or PositionType.PontaDireita or PositionType.Centroavante
            or PositionType.SegundoAtacante => GrupoDePosicao.Ataque,
        _ => GrupoDePosicao.Meio
    };

    /// <summary>
    /// Está entre os <paramref name="titulares"/> melhores do grupo dele no elenco (overall, com a margem de 1 ponto)?
    /// </summary>
    public static bool EntreOsMelhores(int overall, IReadOnlyList<int> overallDosOutrosDoGrupo, int titulares)
    {
        if (titulares <= 0) return false;
        var outros = overallDosOutrosDoGrupo.OrderByDescending(o => o).ToList();
        return outros.Count < titulares || overall >= outros[titulares - 1] - ParticipacaoCriterios.MargemDoNivel;
    }

    /// <summary>Papel de quem acabou de chegar.</summary>
    public static PapelNoElenco PelaChegada(ChegadaAoClube chegada, bool entreOsMelhores, int? idade)
    {
        if (chegada.Leilao is decimal pago)
            return chegada.CompraImediata || pago >= PapelCriterios.LeilaoTitularAPartirDe ? PapelNoElenco.Titular
                : pago > PapelCriterios.LeilaoDisputaAcimaDe ? PapelNoElenco.Disputa
                : PapelNoElenco.Rotacao;

        if (chegada.DraftRodada is int rodada)
        {
            if (rodada == 1)
            {
                if (!entreOsMelhores) return PapelNoElenco.Rotacao;
                var primeiraMetade = chegada.DraftPick is int pick && chegada.DraftTimes is int times && pick <= (times + 1) / 2;
                return primeiraMetade ? PapelNoElenco.Titular : PapelNoElenco.Disputa;
            }
            // Da 2ª rodada em diante o jovem chega para aprender; o experiente (draft para montar elenco), pela posição.
            if (!chegada.Expansao && !(idade > PapelCriterios.IdadeDaPromessaNoDraft)) return PapelNoElenco.Promessa;
        }

        return entreOsMelhores ? PapelNoElenco.Disputa : PapelNoElenco.Rotacao;
    }

    /// <summary>
    /// Papel na temporada nova, pelo que jogou na que acabou (<paramref name="participacao"/> nula = jogos de menos para
    /// dizer: fica o que tinha).
    /// </summary>
    public static PapelNoElenco NaVirada(decimal? participacao, PapelNoElenco atual, int? idade)
    {
        var papel = participacao switch
        {
            decimal p when p >= PapelCriterios.ViradaTitularAPartirDe => PapelNoElenco.Titular,
            decimal p when p >= PapelCriterios.ViradaDisputaAPartirDe => PapelNoElenco.Disputa,
            decimal => PapelNoElenco.Rotacao,
            null => atual == PapelNoElenco.Promessa ? PapelNoElenco.Rotacao : atual
        };

        if (idade >= PapelCriterios.IdadeDoVeterano && papel == PapelNoElenco.Titular) return PapelNoElenco.Disputa;
        if (idade <= PapelCriterios.IdadeDaPromessa && papel != PapelNoElenco.Titular) return PapelNoElenco.Promessa;
        return papel;
    }

    /// <summary>Um degrau abaixo, na conversa (nulo: não tem como descer).</summary>
    public static PapelNoElenco? UmDegrauAbaixo(PapelNoElenco papel) => papel switch
    {
        PapelNoElenco.Titular => PapelNoElenco.Disputa,
        PapelNoElenco.Disputa => PapelNoElenco.Rotacao,
        _ => null
    };
}
