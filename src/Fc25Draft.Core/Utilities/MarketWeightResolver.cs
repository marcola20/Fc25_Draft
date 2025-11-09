using System.Collections.Generic;

namespace Fc25Draft.Core.Utilities;

public static class MarketWeightResolver
{
    private static readonly Dictionary<short, decimal> WeightsById = new()
    {
        { 1, 0.90m },
        { 2, 1.05m },
        { 3, 1.10m },
        { 4, 1.10m },
        { 5, 1.10m },
        { 6, 1.10m },
        { 7, 1.20m },
        { 8, 1.20m },
        { 9, 1.20m },
        { 10, 1.20m }
    };

    private static readonly Dictionary<string, short> CodeToId = new(StringComparer.OrdinalIgnoreCase)
    {
        { "GK", 1 },
        { "CB", 2 },
        { "LB", 3 },
        { "RB", 4 },
        { "CDM", 5 },
        { "CM", 6 },
        { "CAM", 7 },
        { "LW", 8 },
        { "RW", 9 },
        { "ST", 10 }
    };

    public static decimal GetByPositionId(short positionId)
    {
        if (!WeightsById.TryGetValue(positionId, out var weight))
        {
            throw new ArgumentException($"Peso não configurado para a posição {positionId}.", nameof(positionId));
        }

        return weight;
    }

    public static decimal GetByCode(string positionCode)
    {
        if (string.IsNullOrWhiteSpace(positionCode))
        {
            throw new ArgumentException("Código de posição obrigatório.", nameof(positionCode));
        }

        if (!CodeToId.TryGetValue(positionCode.Trim().ToUpperInvariant(), out var positionId))
        {
            throw new ArgumentException($"Código de posição desconhecido: {positionCode}.", nameof(positionCode));
        }

        return GetByPositionId(positionId);
    }
}
