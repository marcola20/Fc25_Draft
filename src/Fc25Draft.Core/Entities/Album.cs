using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Entities;

/// <summary>Figurinha de jogador, do escudo do clube (a que abre a página) ou do técnico/auxiliar.</summary>
public enum TipoFigurinha
{
    Jogador = 1,
    Escudo = 2,

    /// <summary>Técnico ou auxiliar do clube no lançamento. Sorteada como brilhante, com visual próprio.</summary>
    Treinador = 3
}

/// <summary>Raridade da figurinha. Cada uma tem só uma, a mais alta que se aplicar.</summary>
public enum RaridadeFigurinha
{
    Comum = 1,
    Brilhante = 2,
    Lendaria = 3
}

/// <summary>
/// O álbum de figurinhas de uma temporada. Gerado pelo admin a partir dos elencos dos clubes da liga
/// no dia do lançamento; depois disso transferência não mexe nele.
/// </summary>
public class Album
{
    public Guid AlbumId { get; set; }

    public string Nome { get; set; } = null!;

    public int Temporada { get; set; }

    public DateTime LancadoEm { get; set; }

    /// <summary>O álbum que está valendo: é nele que os pacotes abrem. Os antigos ficam como coleção.</summary>
    public bool Ativo { get; set; }

    /// <summary>
    /// X do bolão: a cada X pontos na temporada do álbum a pessoa ganha 1 pacote. Mudar não tira pacote
    /// de ninguém; baixar faz a próxima reconciliação dar os que passaram a ser devidos.
    /// </summary>
    public int PontosBolaoPorPacote { get; set; } = AlbumFigurinhas.PontosBolaoPorPacotePadrao;

    public ICollection<Figurinha> Figurinhas { get; set; } = new List<Figurinha>();
}

/// <summary>
/// Uma figurinha do álbum: retrato do jogador (ou do escudo) no dia do lançamento — nome, posição,
/// overall e clube daquela data.
/// </summary>
public class Figurinha
{
    public Guid FigurinhaId { get; set; }
    public Guid AlbumId { get; set; }

    /// <summary>Numeração corrida do álbum (#001, #002…).</summary>
    public int Numero { get; set; }

    public TipoFigurinha Tipo { get; set; }
    public RaridadeFigurinha Raridade { get; set; }

    /// <summary>Clube da página.</summary>
    public Guid TeamId { get; set; }

    /// <summary>Nulo na figurinha do escudo e na de técnico.</summary>
    public int? PlayerId { get; set; }

    /// <summary>
    /// A pessoa da figurinha de técnico. Clube, papel e nome são o retrato do lançamento; foto, apelido,
    /// frase e esquema vêm do perfil na hora de mostrar.
    /// </summary>
    public Guid? TreinadorId { get; set; }

    /// <summary>Técnico ou auxiliar, no lançamento (só na figurinha de técnico).</summary>
    public PapelTreinador? Papel { get; set; }

    public string NomeImpresso { get; set; } = null!;

    /// <summary>Sigla da posição (GOL, ZAG…); nulo no escudo.</summary>
    public string? PosicaoSigla { get; set; }

    public int? Overall { get; set; }

    /// <summary>Texto curto da lendária, escrito pelo admin ("Artilheiro de 2009").</summary>
    public string? Destaque { get; set; }

    /// <summary>Lugar na página do clube (0 = escudo).</summary>
    public int Ordem { get; set; }

    public Album Album { get; set; } = null!;
    public Team Time { get; set; } = null!;
    public Player? Jogador { get; set; }
    public Treinador? Treinador { get; set; }
}

/// <summary>As figurinhas que a pessoa já tirou. <c>Quantidade - 1</c> são as repetidas.</summary>
public class FigurinhaDoTreinador
{
    public Guid TreinadorId { get; set; }
    public Guid FigurinhaId { get; set; }

    public int Quantidade { get; set; }

    /// <summary>Quando saiu a primeira cópia (a que foi colada).</summary>
    public DateTime PrimeiraEm { get; set; }

    /// <summary>Colada e ainda não vista na página do clube.</summary>
    public bool Nova { get; set; }

    public Treinador Treinador { get; set; } = null!;
    public Figurinha Figurinha { get; set; } = null!;
}

/// <summary>
/// Livro-razão dos pacotes ganhos. A chave é única por pessoa (<c>diario:2026-10-03</c>,
/// <c>jogo:{partida}:2</c>, <c>admin:{guid}</c>…), então reconciliar de novo nunca dá pacote em dobro.
/// O sorteio é feito na hora de abrir, não na de ganhar.
/// </summary>
public class PacoteGanho
{
    public const string OrigemAdmin = "admin";
    public const string OrigemDiario = "diario";
    public const string OrigemJogo = "jogo";
    public const string OrigemDiaDeJogo = "diadejogo";
    public const string OrigemBolao = "bolao";
    public const string OrigemReciclagem = "reciclagem";

    public Guid PacoteId { get; set; }
    public Guid TreinadorId { get; set; }

    /// <summary>Álbum em que o pacote abre; nulo = o álbum ativo na hora de abrir.</summary>
    public Guid? AlbumId { get; set; }

    public string Origem { get; set; } = null!;
    public string Chave { get; set; } = null!;

    /// <summary>Por que ganhou, em palavras (o motivo do admin, "Vitória sobre o Grêmio"…).</summary>
    public string? Motivo { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AbertoEm { get; set; }

    /// <summary>As figurinhas que saíram, na ordem do pacote.</summary>
    public Guid[]? Figurinhas { get; set; }

    public Treinador Treinador { get; set; } = null!;
    public Album? Album { get; set; }
}

public enum TipoConquistaAlbum
{
    /// <summary>Todas as figurinhas de um clube coladas.</summary>
    PaginaCompleta = 1,

    /// <summary>O álbum inteiro colado: vai para o Hall da Fama (Colecionadores).</summary>
    AlbumCompleto = 2
}

/// <summary>
/// Selo do álbum: página completa (com o clube) ou álbum completo. Gravado uma vez só, quando entra a
/// figurinha que completa; não sai mais.
/// </summary>
public class AlbumConquista
{
    public Guid ConquistaId { get; set; }
    public Guid TreinadorId { get; set; }
    public Guid AlbumId { get; set; }
    public TipoConquistaAlbum Tipo { get; set; }

    /// <summary>O clube da página; nulo no álbum completo.</summary>
    public Guid? TeamId { get; set; }

    public DateTime Em { get; set; }

    public Treinador Treinador { get; set; } = null!;
    public Album Album { get; set; } = null!;
    public Team? Time { get; set; }
}

public enum StatusTroca
{
    /// <summary>Esperando a resposta de quem recebeu (até <see cref="TrocaFigurinhas.ExpiraEm"/>).</summary>
    Pendente = 1,
    Aceita = 2,
    Recusada = 3,

    /// <summary>Quem propôs desistiu.</summary>
    Cancelada = 4,

    /// <summary>Passou do prazo sem resposta.</summary>
    Expirada = 5,

    /// <summary>Na hora de aceitar alguém não tinha mais a repetida (o motivo fica em <see cref="TrocaFigurinhas.Motivo"/>).</summary>
    NaoValeMais = 6,

    /// <summary>Quem recebeu respondeu com outra proposta (a nova aponta para esta).</summary>
    Contraproposta = 7
}

/// <summary>
/// Proposta de troca de figurinhas entre duas pessoas. Só repetidas entram (quem dá precisa ter pelo menos
/// 2), então o álbum colado nunca perde figurinha. O aceite move tudo numa transação só.
/// </summary>
public class TrocaFigurinhas
{
    public Guid TrocaId { get; set; }
    public Guid AlbumId { get; set; }

    /// <summary>Quem propôs: dá as <c>Oferecida</c> e recebe as pedidas.</summary>
    public Guid DeTreinadorId { get; set; }

    /// <summary>Quem recebeu a proposta e responde.</summary>
    public Guid ParaTreinadorId { get; set; }

    public StatusTroca Status { get; set; }

    public DateTime CriadaEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? RespondidaEm { get; set; }

    /// <summary>Por que não vale mais (ou outro detalhe do fim da proposta).</summary>
    public string? Motivo { get; set; }

    /// <summary>A proposta que esta responde, quando é contraproposta.</summary>
    public Guid? ContrapropostaDeId { get; set; }

    public Album Album { get; set; } = null!;
    public Treinador De { get; set; } = null!;
    public Treinador Para { get; set; } = null!;
    public ICollection<TrocaFigurinhaItem> Itens { get; set; } = new List<TrocaFigurinhaItem>();
}

/// <summary>Uma figurinha da troca (uma cópia): oferecida por quem propôs ou pedida a quem recebeu.</summary>
public class TrocaFigurinhaItem
{
    public Guid TrocaId { get; set; }
    public Guid FigurinhaId { get; set; }

    /// <summary>Verdadeiro: sai de quem propôs. Falso: sai de quem recebeu (é o que foi pedido).</summary>
    public bool Oferecida { get; set; }

    public TrocaFigurinhas Troca { get; set; } = null!;
    public Figurinha Figurinha { get; set; } = null!;
}
