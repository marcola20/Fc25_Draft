using System;
using Fc25Draft.Core.Entities;

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
