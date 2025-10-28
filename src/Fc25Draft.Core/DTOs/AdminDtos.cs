namespace Fc25Draft.Core.DTOs;

public record AdminAdjustBudgetRequestDto(Guid TeamId, decimal Delta, string Reason);

public record AdminCancelMarketItemRequestDto(string Reason);

public record AdminSellPlayersRequestDto(Guid FromTeamId, Guid ToTeamId, Guid[] PlayerIds, decimal Amount, string Reason);

public record AdminSwapPlayersRequestDto(
    Guid TeamAId,
    Guid[] PlayersFromA,
    Guid TeamBId,
    Guid[] PlayersFromB,
    decimal CashAdjustFromAToB,
    string Reason);

public record AdminMovePlayerRequestDto(Guid PlayerId, Guid ToTeamId, string Reason);
