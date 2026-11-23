using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// O que cada fase do chaveamento é. O número do enum não diz a ordem (o playoff da Copa entrou
/// depois da final), então quem precisa saber "qual fase vem antes" usa <see cref="Nivel"/>.
/// </summary>
public static class FasesKnockout
{
    public static bool EhPlayoff(FaseKnockout fase) => fase is FaseKnockout.PO1 or FaseKnockout.PO2 or FaseKnockout.PO3 or FaseKnockout.PO4;
    public static bool EhQuartas(FaseKnockout fase) => fase is FaseKnockout.QF1 or FaseKnockout.QF2 or FaseKnockout.QF3 or FaseKnockout.QF4;
    public static bool EhSemi(FaseKnockout fase) => fase is FaseKnockout.Semi1 or FaseKnockout.Semi2;
    public static bool EhPlayIn(FaseKnockout fase) => fase is FaseKnockout.PlayIn_A or FaseKnockout.PlayIn_B or FaseKnockout.PlayIn_C;

    /// <summary>Quão longe a fase fica no chaveamento (os jogos de uma mesma fase empatam).</summary>
    public static int Nivel(FaseKnockout fase) => fase switch
    {
        FaseKnockout.Final => 5,
        FaseKnockout.Semi1 or FaseKnockout.Semi2 => 4,
        _ when EhQuartas(fase) => 3,
        FaseKnockout.PlayIn_C => 2,
        _ when EhPlayoff(fase) => 2,
        FaseKnockout.PlayIn_A or FaseKnockout.PlayIn_B => 1,
        _ => 0
    };

    /// <summary>Ordem de exibição: da primeira fase para a final, e dentro da fase pela ordem do enum.</summary>
    public static int Ordem(FaseKnockout fase) => Nivel(fase) * 100 + (int)fase;

    /// <summary>Nome curto da fase ("Playoff", "Quartas de final", ...).</summary>
    public static string Nome(FaseKnockout fase) => fase switch
    {
        _ when EhPlayIn(fase) => "Play-In",
        _ when EhPlayoff(fase) => "Playoff",
        _ when EhQuartas(fase) => "Quartas de final",
        _ when EhSemi(fase) => "Semifinal",
        FaseKnockout.Final => "Final",
        _ => fase.ToString()
    };

    /// <summary>Rótulo do jogo no chaveamento; a Copa em fase de liga cruza por posição na tabela.</summary>
    public static string Rotulo(FaseKnockout fase, bool faseDeLiga) => fase switch
    {
        FaseKnockout.PlayIn_A => "Play-In Jogo 1 (9º vs 10º)",
        FaseKnockout.PlayIn_B => "Play-In Jogo 2 (7º vs 8º)",
        FaseKnockout.PlayIn_C => "Play-In Jogo 3 (Decisivo)",
        FaseKnockout.PO1 => "Playoff 1 — 5º x 12º",
        FaseKnockout.PO2 => "Playoff 2 — 6º x 11º",
        FaseKnockout.PO3 => "Playoff 3 — 7º x 10º",
        FaseKnockout.PO4 => "Playoff 4 — 8º x 9º",
        FaseKnockout.QF1 => faseDeLiga ? "Quartas 1 — 1º x vencedor do Playoff 4" : "Quartas 1 — 1º Grupo A x 2º Grupo B",
        FaseKnockout.QF2 => faseDeLiga ? "Quartas 2 — 2º x vencedor do Playoff 3" : "Quartas 2 — 1º Grupo B x 2º Grupo A",
        FaseKnockout.QF3 => faseDeLiga ? "Quartas 3 — 3º x vencedor do Playoff 2" : "Quartas 3 — 1º Grupo C x 2º Grupo D",
        FaseKnockout.QF4 => faseDeLiga ? "Quartas 4 — 4º x vencedor do Playoff 1" : "Quartas 4 — 1º Grupo D x 2º Grupo C",
        FaseKnockout.Semi1 => "Semifinal 1",
        FaseKnockout.Semi2 => "Semifinal 2",
        FaseKnockout.Final => "Final",
        _ => fase.ToString()
    };
}
