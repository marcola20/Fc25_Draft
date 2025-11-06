using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

public sealed record TransferOfferSummaryDto(
    Guid OfferId,
    Guid ThreadId,
    Guid? CounterOfOfferId,
    TransferOfferStatus Status,
    Guid FromTeamId,
    string FromTeamName,
    Guid ToTeamId,
    string ToTeamName,
    IReadOnlyList<TransferOfferParticipantDto> Targets,
    decimal? OfferedFee,
    decimal? SellOnPercent,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? RespondedAtUtc,
    uint RowVersion,
    IReadOnlyList<TransferOfferSwapPlayerDto> SwapPlayers);

public sealed record TransferOfferDetailDto(
    Guid OfferId,
    Guid ThreadId,
    Guid? CounterOfOfferId,
    TransferOfferStatus Status,
    Guid FromTeamId,
    string FromTeamName,
    Guid ToTeamId,
    string ToTeamName,
    IReadOnlyList<TransferOfferParticipantDto> Targets,
    decimal? OfferedFee,
    decimal? SellOnPercent,
    string? Message,
    string? ResponseMessage,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? RespondedAtUtc,
    uint RowVersion,
    IReadOnlyList<TransferOfferSwapPlayerDto> SwapPlayers);

public sealed record TransferOfferParticipantDto(
    int PlayerId,
    Guid PlayerGuid,
    string Name,
    string Position,
    int Overall);

public sealed record TransferOfferSwapPlayerDto(
    int PlayerId,
    Guid PlayerGuid,
    string Name,
    string Position,
    int Overall);
