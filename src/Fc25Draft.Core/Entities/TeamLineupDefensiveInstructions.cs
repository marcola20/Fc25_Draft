namespace Fc25Draft.Core.Entities;

// DefensiveStyle:   1=Retranca, 2=Pressão na Frente
// ContainmentArea:  1=Ampla, 2=Centro
// Pressure:         1=Tradicional, 2=Agressiva
// DefensiveLine:    1-10
// Density:          1-10
public class TeamLineupDefensiveInstructions
{
    public Guid LineupId { get; set; }
    public int DefensiveStyle { get; set; }
    public int ContainmentArea { get; set; }
    public int Pressure { get; set; }
    public int DefensiveLine { get; set; }
    public int Density { get; set; }

    public TeamLineup Lineup { get; set; } = null!;
}
