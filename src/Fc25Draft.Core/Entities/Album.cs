namespace Fc25Draft.Core.Entities;

/// <summary>Figurinha de jogador ou do escudo do clube (a que abre a página).</summary>
public enum TipoFigurinha
{
    Jogador = 1,
    Escudo = 2
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

    /// <summary>Nulo na figurinha do escudo.</summary>
    public int? PlayerId { get; set; }

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
/// <c>vitoria:{partida}</c>, <c>admin:{guid}</c>…), então reconciliar de novo nunca dá pacote em dobro.
/// O sorteio é feito na hora de abrir, não na de ganhar.
/// </summary>
public class PacoteGanho
{
    public const string OrigemAdmin = "admin";
    public const string OrigemDiario = "diario";
    public const string OrigemVitoria = "vitoria";
    public const string OrigemBolao = "bolao";

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
