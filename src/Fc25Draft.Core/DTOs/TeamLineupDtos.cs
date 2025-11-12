using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record SaveLineupRequest
{
    public Guid? LineupId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string FormationCode { get; init; } = string.Empty;
    public string TacticCode { get; init; } = string.Empty;
    public string? Observation { get; init; }
    public bool SetAsActive { get; init; }
    public LineupSpecialRolesDto SpecialRoles { get; init; } = new();
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
    string Name,
    string FormationCode,
    string TacticCode,
    string? Observation,
    bool IsActive,
    DateTime UpdatedAtUtc,
    LineupSpecialRolesResponse SpecialRoles,
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

public record LineupSpecialRolesDto
{
    public int? CaptainPlayerId { get; init; }
    public int? ShortFreeKickLeftPlayerId { get; init; }
    public int? ShortFreeKickRightPlayerId { get; init; }
    public int? LongFreeKickPlayerId { get; init; }
    public int? PenaltyKickPlayerId { get; init; }
    public int? LeftCornerPlayerId { get; init; }
    public int? RightCornerPlayerId { get; init; }
}

public record LineupSpecialRolesResponse(
    int? CaptainPlayerId,
    string? CaptainPlayerName,
    int? ShortFreeKickLeftPlayerId,
    string? ShortFreeKickLeftPlayerName,
    int? ShortFreeKickRightPlayerId,
    string? ShortFreeKickRightPlayerName,
    int? LongFreeKickPlayerId,
    string? LongFreeKickPlayerName,
    int? PenaltyKickPlayerId,
    string? PenaltyKickPlayerName,
    int? LeftCornerPlayerId,
    string? LeftCornerPlayerName,
    int? RightCornerPlayerId,
    string? RightCornerPlayerName);

public record TeamLineupSummaryResponse(
    Guid LineupId,
    string Name,
    string FormationCode,
    string TacticCode,
    bool IsActive,
    DateTime UpdatedAtUtc);
