namespace Fc25Draft.Core.Entities;

/// <summary>
/// Vídeo com todos os jogos de uma rodada juntos (o mosaico da última rodada das Séries A e B).
/// Tem telão próprio, com chat e os palpites de todas as rodadas que ele mostra.
/// </summary>
public class Mosaico
{
    public Guid MosaicoId { get; set; }
    public string Titulo { get; set; } = null!;
    public string? YoutubeVideoId { get; set; }
    public DateTime CriadoEm { get; set; }

    public ICollection<MosaicoRodada> Rodadas { get; set; } = new List<MosaicoRodada>();
}

/// <summary>Uma rodada que passa no mosaico: costuma ser a última da Série A e a última da Série B.</summary>
public class MosaicoRodada
{
    public Guid MosaicoId { get; set; }
    public Guid RodadaId { get; set; }

    public Mosaico Mosaico { get; set; } = null!;
    public LigaRodada Rodada { get; set; } = null!;
}

/// <summary>Emoji que um treinador deixou no mosaico, como no telão de um jogo.</summary>
public class MosaicoReacao
{
    public Guid MosaicoId { get; set; }
    public Guid TreinadorId { get; set; }
    public string Emoji { get; set; } = null!;
    public DateTime CriadaEm { get; set; }

    public Mosaico Mosaico { get; set; } = null!;
    public Treinador Treinador { get; set; } = null!;
}
