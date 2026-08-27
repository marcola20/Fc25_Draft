namespace Fc25Draft.Core.Utilities;

public static class LineupLabels
{
    public static string AutoSubstitution(int v) => v switch
    {
        1 => "Desativado",
        2 => "Muito Tarde",
        3 => "Flexível",
        4 => "Muito Cedo",
        _ => v.ToString()
    };

    public static string OffensiveStyle(int v) => v switch { 1 => "Contra-ataque", 2 => "Posse de Bola no Ataque", _ => "-" };
    public static string Playmaker(int v) => v switch { 1 => "Passe Longo", 2 => "Passe Curto", _ => "-" };
    public static string AttackArea(int v) => v switch { 1 => "Centro", 2 => "Ampla", _ => "-" };
    public static string Positioning(int v) => v switch { 1 => "Manter Formação", 2 => "Flexível", _ => "-" };
    public static string DefensiveStyle(int v) => v switch { 1 => "Retranca", 2 => "Pressão na Frente", _ => "-" };
    public static string ContainmentArea(int v) => v switch { 1 => "Ampla", 2 => "Centro", _ => "-" };
    public static string Pressure(int v) => v switch { 1 => "Tradicional", 2 => "Agressiva", _ => "-" };

    public static string AdvancedAttack(int v) => v switch
    {
        1 => "Desligado",
        2 => "Ancoragem",
        3 => "Falso Ala",
        4 => "Defensivo",
        5 => "Preso às Laterais",
        6 => "Laterais Ofensivos",
        7 => "Rotação de Alas",
        8 => "Tik-Taka",
        9 => "Falso 9",
        10 => "Alvos de Cruzamento",
        11 => "Falso Lateral",
        _ => v.ToString()
    };

    public static string AdvancedDefense(int v) => v switch
    {
        1 => "Desligado",
        2 => "Lateral Avançado",
        3 => "Defesa Recuada",
        4 => "Invadir a Área",
        5 => "Pivô Contra-ataque",
        6 => "Pressão Ofensiva",
        _ => v.ToString()
    };
}
