namespace Fc25Draft.Core.Enums;

/// <summary>Formato da primeira fase da Copa.</summary>
public enum FormatoCopa
{
    /// <summary>Grupos sorteados por potes, os 2 primeiros de cada grupo vão às quartas (até 2010).</summary>
    Grupos = 0,

    /// <summary>
    /// Fase de liga (a partir de 2011): todos numa tabela só, 4 jogos cada; 1º ao 4º vão direto às
    /// quartas e 5º ao 12º jogam o playoff (5º x 12º, 6º x 11º, 7º x 10º, 8º x 9º).
    /// </summary>
    FaseDeLiga = 1
}
