namespace Fc25Draft.Core.Entities;

/// <summary>Os prêmios de destaque votados no fim da temporada (ver <c>PremiosTemporada</c> para as regras de cada um).</summary>
public enum CategoriaPremio
{
    MVP = 1,
    ROY = 2,
    MIP = 3,
    DPOY = 4,
    GKOY = 5,
    COTY = 6
}

public enum SituacaoVotacao
{
    /// <summary>O admin está montando os finalistas; ninguém vê.</summary>
    Preparando = 0,
    /// <summary>Treinadores e auxiliares votando.</summary>
    Aberta = 1,
    /// <summary>Fechou para voto; o admin confere a apuração e confirma os vencedores.</summary>
    Encerrada = 2,
    /// <summary>Vencedores publicados.</summary>
    Publicada = 3
}

/// <summary>A votação dos prêmios de uma temporada.</summary>
public class VotacaoPremios
{
    public int Temporada { get; set; }
    public SituacaoVotacao Situacao { get; set; }
    public DateTime CriadaEm { get; set; }
    public DateTime? AbertaEm { get; set; }
    public DateTime? EncerradaEm { get; set; }
    public DateTime? PublicadaEm { get; set; }

    public ICollection<PremioCandidato> Candidatos { get; set; } = new List<PremioCandidato>();
}

/// <summary>
/// Um finalista de uma categoria: jogador (ou treinador, no COTY) e o clube dele na temporada — é o clube que
/// impede o voto de quem é de lá.
/// </summary>
public class PremioCandidato
{
    public Guid CandidatoId { get; set; }
    public int Temporada { get; set; }
    public CategoriaPremio Categoria { get; set; }

    public int? PlayerId { get; set; }
    public Guid? TreinadorId { get; set; }
    public Guid? TimeId { get; set; }

    /// <summary>"nota 7,4 · 18 jogos · 9 G/A", para quem vota lembrar a temporada dele.</summary>
    public string? Resumo { get; set; }

    /// <summary>Marcado pelo admin na apuração (a sugestão é o mais votado; empate ele decide).</summary>
    public bool Vencedor { get; set; }

    /// <summary>+1 over do prêmio: quem recebeu (o próprio ou um companheiro, se ele já levou over por outro prêmio).</summary>
    public int? OverParaPlayerId { get; set; }
    public DateTime? OverAplicadoEm { get; set; }

    /// <summary>A mudança de atributos que vai para o save pelo Editor PES; nula se o jogador não tem atributos do jogo.</summary>
    public int? EvolucaoPesId { get; set; }

    public VotacaoPremios Votacao { get; set; } = null!;
    public Player? Player { get; set; }
    public Treinador? Treinador { get; set; }
    public Team? Time { get; set; }
    public Player? OverPara { get; set; }
}

/// <summary>Um voto: a pessoa marcou o candidato. Cada um vale 1 ponto na apuração.</summary>
public class PremioVoto
{
    public Guid VotoId { get; set; }
    public Guid CandidatoId { get; set; }
    public Guid EleitorId { get; set; }
    public DateTime VotadoEm { get; set; }

    public PremioCandidato Candidato { get; set; } = null!;
    public Treinador Eleitor { get; set; } = null!;
}
