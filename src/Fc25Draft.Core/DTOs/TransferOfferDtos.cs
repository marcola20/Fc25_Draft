using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

public sealed record TransferOfferSummaryDto(
    Guid OfferId,
    TransferOfferStatus Status,
    Guid FromTeamId,
    string FromTeamName,
    Guid ToTeamId,
    string ToTeamName,
    TransferOfferParticipantDto Player,
    decimal? OfferedFee,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? RespondedAtUtc,
    uint RowVersion,
    IReadOnlyList<TransferOfferSwapPlayerDto> SwapPlayers);

public sealed record TransferOfferDetailDto(
    Guid OfferId,
    TransferOfferStatus Status,
    Guid FromTeamId,
    string FromTeamName,
    Guid ToTeamId,
    string ToTeamName,
    TransferOfferParticipantDto Player,
    decimal? OfferedFee,
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
