namespace Fc25Draft.Core.DTOs;

public sealed record TeamLineupDto(
    Guid LineupId,
    Guid TeamId,
    string Name,
    string Formation,
    string? TacticCode,
    bool IsActive,
    IReadOnlyList<TeamLineupSlotDto> Starters,
    IReadOnlyList<TeamLineupSlotDto> Bench,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    TeamLineupRolesDto Roles);

public sealed record TeamLineupSlotDto(
    Guid SlotId,
    string SlotCode,
    string DisplayName,
    bool IsBench,
    int Order,
    IReadOnlyList<short> AllowedPositionIds,
    TeamLineupSlotPlayerDto? Player);

public sealed record TeamLineupSlotPlayerDto(
    int PlayerId,
    Guid PlayerGuid,
    string Name,
    string PositionName,
    short PositionId);

public sealed record TeamLineupSlotAssignmentDto(string SlotCode, int? PlayerId);

public sealed record TeamLineupRolesDto(
    TeamLineupSlotPlayerDto? Captain,
    TeamLineupSlotPlayerDto? ShortFreeKickLeft,
    TeamLineupSlotPlayerDto? ShortFreeKickRight,
    TeamLineupSlotPlayerDto? LongFreeKick,
    TeamLineupSlotPlayerDto? Penalties,
    TeamLineupSlotPlayerDto? CornerLeft,
    TeamLineupSlotPlayerDto? CornerRight);

public sealed record TeamLineupRoleAssignmentsDto(
    int? CaptainPlayerId,
    int? ShortFreeKickLeftPlayerId,
    int? ShortFreeKickRightPlayerId,
    int? LongFreeKickPlayerId,
    int? PenaltiesPlayerId,
    int? CornerLeftPlayerId,
    int? CornerRightPlayerId);

public sealed record TeamLineupSaveRequestDto(
    string Name,
    string Formation,
    string? TacticCode,
    bool IsActive,
    IReadOnlyList<TeamLineupSlotAssignmentDto> Starters,
    IReadOnlyList<TeamLineupSlotAssignmentDto> Bench,
    TeamLineupRoleAssignmentsDto? Roles);

public sealed record AdminLineupOverviewDto(
    Guid LineupId,
    Guid TeamId,
    string TeamName,
    string LineupName,
    string Formation,
    string? TacticCode,
    bool IsActive,
    IReadOnlyList<TeamLineupSlotDto> Starters,
    IReadOnlyList<TeamLineupSlotDto> Bench,
    TeamLineupRolesDto Roles,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
