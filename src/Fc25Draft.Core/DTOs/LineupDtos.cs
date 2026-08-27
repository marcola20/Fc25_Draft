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
    TeamLineupDefensiveInstructionsDto? DefensiveInstructions,
    TeamLineupAdvancedInstructionsDto? AdvancedInstructions);

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

// Attack1/Attack2: 1=Desligado, 2=Ancoragem**, 3=Falso Ala, 4=Defensivo**,
//   5=Preso às Laterais, 6=Laterais Ofensivos, 7=Rotação de alas,
//   8=Tik-Taka, 9=Falso 9, 10=Alvos de Cruzamento, 11=Falso Lateral
// Defense1/Defense2: 1=Desligado, 2=Lateral Avançado, 3=Defesa Recuada,
//   4=Invadir a Área, 5=Pivô contra-ataque**, 6=Pressão Ofensiva
public sealed record TeamLineupAdvancedInstructionsDto(
    int Attack1,
    int? AttackPlayer1Id,
    int Attack2,
    int? AttackPlayer2Id,
    int Defense1,
    int? DefensePlayer1Id,
    int Defense2,
    int? DefensePlayer2Id,
    TeamLineupSlotPlayerDto? AttackPlayer1,
    TeamLineupSlotPlayerDto? AttackPlayer2,
    TeamLineupSlotPlayerDto? DefensePlayer1,
    TeamLineupSlotPlayerDto? DefensePlayer2);

public sealed record TeamLineupAdvancedInstructionsSaveDto(
    int Attack1,
    int? AttackPlayer1Id,
    int Attack2,
    int? AttackPlayer2Id,
    int Defense1,
    int? DefensePlayer1Id,
    int Defense2,
    int? DefensePlayer2Id);

public sealed record TeamLineupSaveRequestDto(
    string Name,
    string Formation,
    int AutoSubstitution,
    bool IsActive,
    IReadOnlyList<TeamLineupSlotAssignmentDto> Starters,
    IReadOnlyList<TeamLineupSlotAssignmentDto> Bench,
    TeamLineupRoleAssignmentsDto? Roles,
    TeamLineupOffensiveInstructionsDto? OffensiveInstructions,
    TeamLineupDefensiveInstructionsDto? DefensiveInstructions,
    TeamLineupAdvancedInstructionsSaveDto? AdvancedInstructions);

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
    TeamLineupAdvancedInstructionsDto? AdvancedInstructions,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    LineupChangeLogDto? LastChange = null);

// Uma entrada de diff entre a versão anterior e a atual de uma escalação.
// Category+Field identifica unicamente o campo (ex.: "Instrução ofensiva"/"Estilo",
// "Titular"/"Lateral Esquerdo") — usado tanto para montar o resumo textual quanto
// para destacar visualmente o item correspondente na tela de detalhes.
public sealed record LineupChangeEntryDto(
    string Category,
    string Field,
    string? OldValue,
    string? NewValue);

// Diff entre o último retrato marcado como "visto" pelo ADM e o estado atual da
// escalação — pode acumular várias edições do time, e mudanças que se cancelam
// (ex.: trocou o titular e trocou de volta) simplesmente não aparecem aqui.
public sealed record LineupChangeLogDto(
    DateTime? LastSeenAtUtc,
    IReadOnlyList<LineupChangeEntryDto> Changes);
