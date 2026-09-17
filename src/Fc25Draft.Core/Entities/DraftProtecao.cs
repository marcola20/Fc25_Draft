namespace Fc25Draft.Core.Entities;

/// <summary>Jogador protegido por um time antes de um draft de expansão (não pode ser escolhido).</summary>
public class DraftProtecao
{
    public Guid DraftId { get; set; }
    public Guid TeamId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>Falso quando o sistema protegeu automaticamente (time não enviou a lista a tempo).</summary>
    public bool EscolhidoPeloTime { get; set; }

    public DateTime CriadoEm { get; set; }

    public Draft Draft { get; set; } = null!;
    public Team Team { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
