using System;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

public record TransferHistoryItemDto(
    Guid TransferId,
    DateTime OccurredAtUtc,
    int PlayerId,
    string PlayerName,
    Guid? FromTeamId,
    string? FromTeamName,
    Guid? ToTeamId,
    string? ToTeamName,
    decimal? Amount,
    decimal? Payout,
    int? OldOverall,
    int? NewOverall,
    int Type,
    string Tipo,
    string? Notes,
    string? PerformedBy);

public record TransferHistoryDto(
    Guid TransferId,
    DateTime OccurredAtUtc,
    Guid PlayerExternalId,
    int PlayerId,
    string PlayerName,
    Guid? FromTeamId,
    string? FromTeamName,
    Guid? ToTeamId,
    string? ToTeamName,
    decimal? Amount,
    decimal? Payout,
    int? OldOverall,
    int? NewOverall,
    int Type,
    string Tipo,
    string? Notes,
    string? PerformedBy);

public class TransferListItemDto
{
    public Guid TransferId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string FromTeamName { get; set; } = string.Empty;
    public string ToTeamName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Payout { get; set; }
    public int? OldOverall { get; set; }
    public int? NewOverall { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public class TransferDetailsDto
{
    public Guid TransferId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public Guid PlayerExternalId { get; set; }
    public int PlayerId { get; set; }
    public Guid? FromTeamId { get; set; }
    public string? FromTeamName { get; set; }
    public Guid? ToTeamId { get; set; }
    public string? ToTeamName { get; set; }
    public decimal Amount { get; set; }
    public decimal? Payout { get; set; }
    public int? OldOverall { get; set; }
    public int? NewOverall { get; set; }
    public string? Notes { get; set; }
    public string? PerformedBy { get; set; }
}

public record RegisterTransferHistoryRequestDto(
    Guid? TransferId,
    int PlayerId,
    Guid? FromTeamId,
    Guid? ToTeamId,
    decimal? Amount,
    decimal? Payout,
    int? OldOverall,
    int? NewOverall,
    TransferType Type,
    DateTime? OccurredAtUtc,
    string? Notes,
    string? PerformedBy);
