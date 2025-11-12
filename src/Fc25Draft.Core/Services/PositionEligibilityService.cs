using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;

namespace Fc25Draft.Core.Services;

public sealed class PositionEligibilityService : IPositionEligibilityService
{
    private static readonly IReadOnlyDictionary<int, IReadOnlySet<int>> EligibilityMap =
        new Dictionary<int, IReadOnlySet<int>>
        {
            [(int)PositionType.Goleiro] = ImmutableHashSet.Create((int)PositionType.Goleiro),
            [(int)PositionType.Zagueiro] = ImmutableHashSet.Create(
                (int)PositionType.Zagueiro,
                (int)PositionType.LateralAlaEsquerdo,
                (int)PositionType.LateralAlaDireito,
                (int)PositionType.Volante),
            [(int)PositionType.LateralAlaEsquerdo] = ImmutableHashSet.Create(
                (int)PositionType.Zagueiro,
                (int)PositionType.LateralAlaEsquerdo,
                (int)PositionType.LateralAlaDireito,
                (int)PositionType.Volante),
            [(int)PositionType.LateralAlaDireito] = ImmutableHashSet.Create(
                (int)PositionType.Zagueiro,
                (int)PositionType.LateralAlaEsquerdo,
                (int)PositionType.LateralAlaDireito,
                (int)PositionType.Volante),
            [(int)PositionType.Volante] = ImmutableHashSet.Create(
                (int)PositionType.Volante,
                (int)PositionType.MeiaCentral),
            [(int)PositionType.MeiaCentral] = ImmutableHashSet.Create(
                (int)PositionType.Volante,
                (int)PositionType.MeiaCentral,
                (int)PositionType.MeiaAtacante),
            [(int)PositionType.MeiaAtacante] = ImmutableHashSet.Create(
                (int)PositionType.MeiaCentral,
                (int)PositionType.MeiaAtacante,
                (int)PositionType.MeiaPontaEsquerda,
                (int)PositionType.MeiaPontaDireita,
                (int)PositionType.Atacante),
            [(int)PositionType.MeiaPontaEsquerda] = ImmutableHashSet.Create(
                (int)PositionType.MeiaAtacante,
                (int)PositionType.MeiaPontaEsquerda,
                (int)PositionType.MeiaPontaDireita,
                (int)PositionType.Atacante),
            [(int)PositionType.MeiaPontaDireita] = ImmutableHashSet.Create(
                (int)PositionType.MeiaAtacante,
                (int)PositionType.MeiaPontaEsquerda,
                (int)PositionType.MeiaPontaDireita,
                (int)PositionType.Atacante),
            [(int)PositionType.Atacante] = ImmutableHashSet.Create(
                (int)PositionType.MeiaAtacante,
                (int)PositionType.MeiaPontaEsquerda,
                (int)PositionType.MeiaPontaDireita,
                (int)PositionType.Atacante)
        };

    private static readonly IReadOnlySet<int> AllPositions =
        Enum.GetValues<PositionType>().Select(p => (int)p).ToImmutableHashSet();

    public IReadOnlySet<int> GetEligiblePositionIdsFor(int primaryPositionId)
    {
        return EligibilityMap.TryGetValue(primaryPositionId, out var eligible)
            ? eligible
            : ImmutableHashSet<int>.Empty;
    }

    public bool IsEligible(int slotPrimaryPositionId, int playerPrimaryPositionId, IReadOnlyCollection<int>? secondaryPositionIds = null)
    {
        if (!EligibilityMap.TryGetValue(slotPrimaryPositionId, out var eligiblePositions))
        {
            return false;
        }

        if (eligiblePositions.Contains(playerPrimaryPositionId))
        {
            return true;
        }

        if (secondaryPositionIds is null || secondaryPositionIds.Count == 0)
        {
            return false;
        }

        return secondaryPositionIds.Any(eligiblePositions.Contains);
    }

    public IReadOnlySet<int> GetAllPositionIds() => AllPositions;
}
