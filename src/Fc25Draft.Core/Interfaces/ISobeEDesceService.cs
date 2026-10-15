using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Quem mais valorizou e desvalorizou no período, pela regra de participação.</summary>
public interface ISobeEDesceService
{
    /// <param name="dias">Tamanho do período, contado para trás a partir de agora.</param>
    /// <param name="quantos">Quantos jogadores em cada lista.</param>
    Task<SobeEDesceDto> GetAsync(int dias, int quantos, CancellationToken ct);
}
