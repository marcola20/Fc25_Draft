using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record SaveLineupRequest
{
    public string FormationCode { get; init; } = string.Empty;
    public string TacticCode { get; init; } = string.Empty;
    public List<SaveLineupSlotDto> Slots { get; init; } = new();
}

public record SaveLineupSlotDto
{
    public int Order { get; init; }
    public byte Role { get; init; }
    public int PrimaryPositionId { get; init; }
    public int? PlayerId { get; init; }
}

public record TeamLineupResponse(
    Guid LineupId,
    Guid TeamId,
    string FormationCode,
    string TacticCode,
    bool IsActive,
    DateTime UpdatedAtUtc,
    IReadOnlyList<LineupSlotResponse> Slots);

public record LineupSlotResponse(
    Guid SlotId,
    int Order,
    byte Role,
    int PrimaryPositionId,
    int? PlayerId,
    string PositionLabel,
    string? PlayerName);

public record LineupSlotTemplateDto(
    int Order,
    byte Role,
    int PrimaryPositionId,
    string PositionLabel);
