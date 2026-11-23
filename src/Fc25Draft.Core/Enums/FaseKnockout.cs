namespace Fc25Draft.Core.Enums;

/// <summary>
/// Slots fixos do bracket conforme o /formato:
/// PlayIn_A: 9º vs 10º | PlayIn_B: 7º vs 8º | PlayIn_C: Vencedor(A) vs Perdedor(B)
/// QF1: 1º vs Vencedor(C) | QF2: 2º vs Vencedor(B) | QF3: 3º vs 6º | QF4: 4º vs 5º
/// Semi1: Vencedor(QF1) vs Vencedor(QF4) | Semi2: Vencedor(QF2) vs Vencedor(QF3)
/// Final: Vencedor(Semi1) vs Vencedor(Semi2)
/// Copa em fase de liga (2011+): PO1 5º x 12º | PO2 6º x 11º | PO3 7º x 10º | PO4 8º x 9º;
/// QF1 1º x Vencedor(PO4) | QF2 2º x Vencedor(PO3) | QF3 3º x Vencedor(PO2) | QF4 4º x Vencedor(PO1).
/// </summary>
public enum FaseKnockout
{
    None = 0,
    PlayIn_A = 1,
    PlayIn_B = 2,
    PlayIn_C = 3,
    QF1 = 4,
    QF2 = 5,
    QF3 = 6,
    QF4 = 7,
    Semi1 = 8,
    Semi2 = 9,
    Final = 10,

    /// <summary>Playoff da fase de liga da Copa (antes das quartas).</summary>
    PO1 = 11,
    PO2 = 12,
    PO3 = 13,
    PO4 = 14
}
