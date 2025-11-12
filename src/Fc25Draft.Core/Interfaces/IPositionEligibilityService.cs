using System.Collections.Generic;

namespace Fc25Draft.Core.Interfaces;

public interface IPositionEligibilityService
{
    IReadOnlySet<int> GetEligiblePositionIdsFor(int primaryPositionId);

    bool IsEligible(int slotPrimaryPositionId, int playerPrimaryPositionId, IReadOnlyCollection<int>? secondaryPositionIds = null);

    IReadOnlySet<int> GetAllPositionIds();
}
