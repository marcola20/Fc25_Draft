namespace Fc25Draft.Core.DTOs;

public sealed record TeamLineupDto(
    Guid LineupId,
    Guid TeamId,
    string Name,
    string Formation,
    int AutoSubstitution,
    bool IsActive,
    IReadOnlyList<TeamLineupSlotDto> Starters,
    IReadOnlyList<TeamLineupSlotDto> Bench,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    TeamLineupRolesDto Roles,
    TeamLineupOffensiveInstructionsDto? OffensiveInstructions,
    TeamLineupDefensiveInstructionsDto? DefensiveInstructions);

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
    TeamLineupSlotPlayerDto? ShortFreeKick1,
    TeamLineupSlotPlayerDto? ShortFreeKick2,
    TeamLineupSlotPlayerDto? LongFreeKick,
    TeamLineupSlotPlayerDto? Penalties,
    TeamLineupSlotPlayerDto? CornerLeft,
    TeamLineupSlotPlayerDto? CornerRight,
    TeamLineupSlotPlayerDto? AttackingPlayer1,
    TeamLineupSlotPlayerDto? AttackingPlayer2,
    TeamLineupSlotPlayerDto? AttackingPlayer3);

public sealed record TeamLineupRoleAssignmentsDto(
    int? CaptainPlayerId,
    int? ShortFreeKick1PlayerId,
    int? ShortFreeKick2PlayerId,
    int? LongFreeKickPlayerId,
    int? PenaltiesPlayerId,
    int? CornerLeftPlayerId,
    int? CornerRightPlayerId,
    int? AttackingPlayer1Id,
    int? AttackingPlayer2Id,
    int? AttackingPlayer3Id);

// OffensiveStyle: 1=Contra-ataque, 2=Posse de Bola no Ataque
// Playmaker:      1=Passe Longo, 2=Passe Curto
// AttackArea:     1=Centro, 2=Ampla
// Positioning:    1=Manter Formação, 2=Flexível
// SupportRange:   1-10
public sealed record TeamLineupOffensiveInstructionsDto(
    int OffensiveStyle,
    int Playmaker,
    int AttackArea,
    int Positioning,
    int SupportRange);

// DefensiveStyle:   1=Retranca, 2=Pressão na Frente
// ContainmentArea:  1=Ampla, 2=Centro
// Pressure:         1=Tradicional, 2=Agressiva
// DefensiveLine:    1-10
// Density:          1-10
public sealed record TeamLineupDefensiveInstructionsDto(
    int DefensiveStyle,
    int ContainmentArea,
    int Pressure,
    int DefensiveLine,
    int Density);

public sealed record TeamLineupSaveRequestDto(
    string Name,
    string Formation,
    int AutoSubstitution,
    bool IsActive,
    IReadOnlyList<TeamLineupSlotAssignmentDto> Starters,
    IReadOnlyList<TeamLineupSlotAssignmentDto> Bench,
    TeamLineupRoleAssignmentsDto? Roles,
    TeamLineupOffensiveInstructionsDto? OffensiveInstructions,
    TeamLineupDefensiveInstructionsDto? DefensiveInstructions);

public sealed record AdminLineupOverviewDto(
    Guid LineupId,
    Guid TeamId,
    string TeamName,
    string LineupName,
    string Formation,
    int AutoSubstitution,
    bool IsActive,
    IReadOnlyList<TeamLineupSlotDto> Starters,
    IReadOnlyList<TeamLineupSlotDto> Bench,
    TeamLineupRolesDto Roles,
    TeamLineupOffensiveInstructionsDto? OffensiveInstructions,
    TeamLineupDefensiveInstructionsDto? DefensiveInstructions,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
