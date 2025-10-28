namespace Fc25Draft.Core.DTOs;

public record AdminAdjustBudgetRequestDto(Guid TeamId, decimal Delta, string Reason);

public record AdminCancelMarketItemRequestDto(string Reason);

public record AdminSellPlayersRequestDto(Guid FromTeamId, Guid ToTeamId, Guid[] PlayerIds, decimal Amount, string Reason);
