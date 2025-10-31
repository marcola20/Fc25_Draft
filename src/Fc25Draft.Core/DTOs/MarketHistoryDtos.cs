namespace Fc25Draft.Core.DTOs;

using Fc25Draft.Core.Enums;

public sealed record MarketHistoryFilter
{
    public Guid? CycleId { get; init; }
    public Guid? ItemId { get; init; }
    public int? PlayerId { get; init; }
    public string? PlayerName { get; init; }
    public string? TeamName { get; init; }
    public string? TargetTeamName { get; init; }
    public MarketTransactionType? Type { get; init; }
    public string? PerformedBy { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record MarketTransactionDto(
    Guid TransactionId,
    Guid CycleId,
    Guid ItemId,
    int PlayerId,
    string PlayerName,
    string PositionName,
    Guid? TeamId,
    string? TeamName,
    Guid? TargetTeamId,
    string? TargetTeamName,
    MarketTransactionType Type,
    string TypeName,
    decimal? Amount,
    string PerformedBy,
    string? Notes,
    DateTime OccurredAtUtc);
