using System;
using System.Collections.Generic;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Infra.Services;

internal static class QuickSellOverallCalculator
{
    /// <summary>A venda rápida paga 80% do preço base. A aposentadoria paga o mesmo ao clube.</summary>
    public const decimal FatorDoPagamento = 0.8m;

    public static decimal Pagamento(decimal precoBase) =>
        decimal.Round(precoBase * FatorDoPagamento, 2, MidpointRounding.AwayFromZero);

    /// <summary>O novo overall pela tabela de evolução (editada em /admin/venda-rapida), no máximo 99.</summary>
    public static int CalculateNewOverall(int currentOverall, IReadOnlyList<EvolucaoVendaRapidaDto> tabela)
        => CalculateNewOverall(currentOverall, tabela, RandomInclusive);

    public static int CalculateNewOverall(int currentOverall, IReadOnlyList<EvolucaoVendaRapidaDto> tabela, Func<int, int, int> randomInclusive)
    {
        var delta = CalculateDelta(currentOverall, tabela, randomInclusive);
        return Math.Min(99, currentOverall + delta);
    }

    public static int CalculateDelta(int currentOverall, IReadOnlyList<EvolucaoVendaRapidaDto> tabela, Func<int, int, int> randomInclusive)
    {
        if (randomInclusive is null)
            throw new ArgumentNullException(nameof(randomInclusive));

        var (min, max) = EvolucaoNaVendaRapida.Subida(currentOverall, tabela);
        return max <= 0 ? 0 : randomInclusive(min, max);
    }

    public static int RandomInclusive(int min, int max)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(min), "min cannot be greater than max");

        return Random.Shared.Next(min, max + 1);
    }
}
