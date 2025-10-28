namespace Fc25Draft.Core.DTOs;

public record TransferResult(bool Ok, string Message);

public record AdjustBudgetResult(bool Ok, string Message, decimal NewBudget);

public record CancelItemResult(bool Ok, string Message);

public record TransfersFilter(
    Guid? TeamId,
    Guid? PlayerId,
    int? Type,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 50);

public record TransferHistoryDto(
    Guid TransferId,
    int Type,
    Guid PlayerId,
    string PlayerName,
    Guid? FromTeamId,
    string? FromTeamName,
    Guid? ToTeamId,
    string? ToTeamName,
    decimal? Amount,
    string? Notes,
    DateTime PerformedAtUtc);
