using Fc25Draft.Core.Entities;
using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Fc25Draft.Core.DTOs;

public record TransferHistoryItemDto(
    Guid TransferId,
    DateTime PerformedAtUtc,
    int PlayerId,
    string PlayerName,
    Guid? FromTeamId,
    string? FromTeamName,
    Guid? ToTeamId,
    string? ToTeamName,
    decimal? Amount,
    int Type,
    string Tipo,
    string? Notes,
    string? PerformedBy);

public record TransferHistoryDto(
    Guid TransferId,
    DateTime PerformedAtUtc,
    Guid PlayerExternalId,
    int PlayerId,
    string PlayerName,
    Guid? FromTeamId,
    string? FromTeamName,
    Guid? ToTeamId,
    string? ToTeamName,
    decimal? Amount,
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
    public string? Notes { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public record RegisterTransferHistoryRequestDto(
    Guid? TransferId,
    int PlayerId,
    Guid? FromTeamId,
    Guid? ToTeamId,
    decimal? Amount,
    TransferType Type,
    DateTime? PerformedAtUtc,
    string? Notes,
    string? PerformedBy);
