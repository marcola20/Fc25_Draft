namespace Fc25Draft.Core.Entities;

public enum TipoPedidoArte
{
    /// <summary>Vencedor de um prêmio da temporada publicado.</summary>
    Premio = 1,
    /// <summary>Contratação (ou empréstimo) que passou do valor ou do over mínimo.</summary>
    Contratacao = 2,
    /// <summary>Uma das 3 primeiras escolhas de um draft, sozinha. ReferenciaId = DraftId; a chave diz qual.</summary>
    DraftEscolha = 3,
    /// <summary>As escolhas 4 a 8 de um draft, numa arte só, quando a 8ª sai. ReferenciaId = DraftId.</summary>
    DraftGrupo = 4,
    /// <summary>Seleção da rodada (4-3-3 pela nota do PES) de uma liga, quando todos os jogos dela encerram. ReferenciaId = RodadaId.</summary>
    SelecaoRodada = 5,
    /// <summary>Jogador que se aposentou (na virada). ReferenciaId = PlayerGuid.</summary>
    Aposentadoria = 7,
    /// <summary>3+ gols de um jogador num jogo encerrado. ReferenciaId = PartidaId; o jogador vai na chave.</summary>
    HatTrick = 8,
    /// <summary>Campeão de uma competição (entrada automática do Hall da Fama). ReferenciaId = LigaId.</summary>
    Campeao = 9,
    /// <summary>Quem sobe e quem cai entre Série A e B, com tudo decidido. ReferenciaId = LigaId da Série A.</summary>
    Acesso = 10,
    /// <summary>Artilheiro e garçom (top 3) de uma competição, quando os prêmios de estatística são pagos. ReferenciaId = LigaId.</summary>
    Artilharia = 11,
    /// <summary>Jogador que a organização colocou no Hall da Fama. ReferenciaId = HallDaFamaJogadorId.</summary>
    HallDaFama = 12
}

public enum SituacaoPedidoArte
{
    /// <summary>Na fila: o Artista CBFV (agente no PC do admin) pega na próxima busca.</summary>
    Pendente = 0,
    /// <summary>O agente fez a arte e mandou no Telegram.</summary>
    Feito = 1,
    /// <summary>O agente tentou e não conseguiu; o admin pode mandar refazer.</summary>
    Falhou = 2,
    /// <summary>O admin tirou da fila.</summary>
    Descartado = 3
}

/// <summary>
/// Uma arte que o site pediu ao Artista CBFV. O site só põe na fila (com a referência ao prêmio ou à
/// transferência); os dados da arte são montados na hora em que o agente busca, sempre atualizados.
/// </summary>
public class PedidoArte
{
    public Guid PedidoArteId { get; set; }

    /// <summary>
    /// "premio:{CandidatoId}", "transferencia:{TransferId}", "draft:{DraftId}:{escolha}", "draft:{DraftId}:4-8" ou
    /// "selecao:{RodadaId}":
    /// o mesmo fato nunca gera dois pedidos.
    /// </summary>
    public string Chave { get; set; } = null!;

    public TipoPedidoArte Tipo { get; set; }

    /// <summary>CandidatoId do prêmio, TransferId da transferência ou DraftId do draft.</summary>
    public Guid ReferenciaId { get; set; }

    /// <summary>Para a lista do admin: "MVP 2010 · Ronaldo", "Ronaldo → Flamengo (45 mi)".</summary>
    public string Titulo { get; set; } = null!;

    public SituacaoPedidoArte Situacao { get; set; }
    public int Tentativas { get; set; }
    public string? Erro { get; set; }

    public DateTime CriadoEmUtc { get; set; }
    public DateTime? EntregueEmUtc { get; set; }
    public DateTime? ConcluidoEmUtc { get; set; }
}

/// <summary>Configuração das artes automáticas (uma linha só, Id = 1). Sem a linha, vale desligado.</summary>
public class ArtesConfig
{
    public int Id { get; set; } // singleton, sempre 1

    public bool Ligado { get; set; }

    /// <summary>Contratação com valor a partir disso entra (leilão, venda, compra do empréstimo).</summary>
    public decimal ValorMinimo { get; set; } = 60_000_000m;

    /// <summary>Contratação de jogador com over a partir disso entra, qualquer que seja o valor (troca e empréstimo também).</summary>
    public int OverMinimo { get; set; } = 87;

    /// <summary>Só transferências depois disso (a hora em que ligou pela primeira vez): nada de arte de coisa antiga.</summary>
    public DateTime? TransferenciasDesdeUtc { get; set; }
}
