namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionRoundGenerationCommand(
    bool IncludeReturnLeg,
    DateTime? FirstRoundDateUtc,
    int? DaysBetweenRounds);
