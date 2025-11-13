namespace Fc25Draft.Core.DTOs;

public sealed record TeamLineupDto(
    Guid LineupId,
    Guid TeamId,
    string Name,
    string Formation,
    bool IsActive,
    IReadOnlyList<TeamLineupSlotDto> Starters,
    IReadOnlyList<TeamLineupSlotDto> Bench,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

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

public sealed record TeamLineupSaveRequestDto(
    string Name,
    string Formation,
    bool IsActive,
    IReadOnlyList<TeamLineupSlotAssignmentDto> Starters,
    IReadOnlyList<TeamLineupSlotAssignmentDto> Bench);
