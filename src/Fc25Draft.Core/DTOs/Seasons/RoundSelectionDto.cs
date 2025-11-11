using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs.Seasons;

public sealed record RoundSelectionDto(
    Guid RoundSelectionId,
    Guid RoundId,
    DateTime CreatedAtUtc,
    IReadOnlyList<RoundSelectionPlayerDto> Players);

public sealed record RoundSelectionPlayerDto(
    Guid PlayerGuid,
    int PlayerId,
    string PlayerName,
    string PositionName,
    int PositionOrder);
