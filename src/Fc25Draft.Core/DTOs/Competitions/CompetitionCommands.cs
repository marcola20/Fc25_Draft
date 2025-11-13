using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionCreateCommand(
    Guid SeasonId,
    string Name,
    int Order,
    CompetitionType Type,
    bool IsActive);

public sealed record CompetitionUpdateCommand(
    string Name,
    int Order,
    CompetitionType Type,
    bool IsActive);

public sealed record CompetitionTeamAssignCommand(
    Guid TeamId,
    decimal? InitialBudget,
    string? Notes);

public sealed record CompetitionMatchUpsertCommand(
    Guid CompetitionMatchId,
    Guid CompetitionId,
    Guid RoundId,
    Guid HomeCompetitionTeamId,
    Guid AwayCompetitionTeamId,
    DateTime? MatchDateUtc,
    int? HomeGoals,
    int? AwayGoals,
    CompetitionMatchStatus Status,
    string? Stadium,
    string? Observations);

public sealed record CompetitionMatchEventUpsertCommand(
    Guid? CompetitionMatchEventId,
    Guid CompetitionTeamId,
    int? PlayerId,
    int? RelatedPlayerId,
    CompetitionMatchEventType EventType,
    int? Minute,
    string? Observations);
