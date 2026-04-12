namespace Fc25Draft.Core.Entities;

// Attack1/Attack2: 1=Desligado, 2=Ancoragem**, 3=Falso Ala, 4=Defensivo**,
//   5=Preso às Laterais, 6=Laterais Ofensivos, 7=Rotação de alas,
//   8=Tik-Taka, 9=Falso 9, 10=Alvos de Cruzamento, 11=Falso Lateral
// ** = requer jogador selecionado (AttackPlayer1Id / AttackPlayer2Id)
//
// Defense1/Defense2: 1=Desligado, 2=Lateral Avançado, 3=Defesa Recuada,
//   4=Invadir a Área, 5=Pivô contra-ataque**, 6=Pressão Ofensiva
// ** = requer jogador selecionado (DefensePlayer1Id / DefensePlayer2Id)
public class TeamLineupAdvancedInstructions
{
    public Guid LineupId { get; set; }

    public int Attack1 { get; set; }
    public int? AttackPlayer1Id { get; set; }
    public int Attack2 { get; set; }
    public int? AttackPlayer2Id { get; set; }

    public int Defense1 { get; set; }
    public int? DefensePlayer1Id { get; set; }
    public int Defense2 { get; set; }
    public int? DefensePlayer2Id { get; set; }

    public TeamLineup Lineup { get; set; } = null!;
    public Player? AttackPlayer1 { get; set; }
    public Player? AttackPlayer2 { get; set; }
    public Player? DefensePlayer1 { get; set; }
    public Player? DefensePlayer2 { get; set; }
}
