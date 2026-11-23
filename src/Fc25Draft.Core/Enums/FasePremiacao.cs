namespace Fc25Draft.Core.Enums;

/// <summary>Até onde o time chegou numa competição de mata-mata (Copa, Supercopa).</summary>
public enum FasePremiacao
{
    Campeao = 1,
    Vice = 2,
    /// <summary>Eliminado nas semifinais.</summary>
    Semifinal = 3,
    /// <summary>Eliminado nas quartas.</summary>
    Quartas = 4,
    /// <summary>Eliminado ainda na fase de grupos (ou na fase de liga, na Copa de 2011 em diante).</summary>
    FaseDeGrupos = 5,
    /// <summary>
    /// Eliminado no playoff da Copa em fase de liga (5º ao 12º). Fica entre as quartas e a fase de liga:
    /// a ordem certa está em <c>PremiacaoLabels.Ordem</c>, não no número.
    /// </summary>
    Playoff = 6
}
