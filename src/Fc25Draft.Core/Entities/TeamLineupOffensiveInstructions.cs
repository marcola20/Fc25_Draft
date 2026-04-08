namespace Fc25Draft.Core.Entities;

// OffensiveStyle: 1=Contra-ataque, 2=Posse de Bola no Ataque
// Playmaker:     1=Passe Longo, 2=Passe Curto
// AttackArea:    1=Centro, 2=Ampla
// Positioning:   1=Manter Formação, 2=Flexível
// SupportRange:  1-10
public class TeamLineupOffensiveInstructions
{
    public Guid LineupId { get; set; }
    public int OffensiveStyle { get; set; }
    public int Playmaker { get; set; }
    public int AttackArea { get; set; }
    public int Positioning { get; set; }
    public int SupportRange { get; set; }

    public TeamLineup Lineup { get; set; } = null!;
}
