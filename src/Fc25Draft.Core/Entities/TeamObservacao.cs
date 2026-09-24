namespace Fc25Draft.Core.Entities;

/// <summary>Jogador que um time marcou para acompanhar (lista de observação da Minha Área).</summary>
public class TeamObservacao
{
    public Guid TeamId { get; set; }
    public int PlayerId { get; set; }
    public DateTime CriadoEm { get; set; }

    public Team Team { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
