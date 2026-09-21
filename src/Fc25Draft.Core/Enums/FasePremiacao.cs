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
    /// <summary>Eliminado ainda na fase de grupos.</summary>
    FaseDeGrupos = 5
}
