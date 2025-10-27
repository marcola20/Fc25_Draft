namespace Fc25Draft.Core.DTOs;

public record DraftStateDto(
    Guid? DraftId,
    string? DraftName,
    int TotalTeams,
    int TotalRounds,
    int TotalPicks,
    int CompletedPicks,
    int? CurrentRound,
    int? CurrentPickInRound,
    int? CurrentOverallPick,
    Guid? CurrentTeamId,
    string? CurrentTeamName,
    string? CurrentTeamOwner,
    Guid? NextTeamId,
    string? NextTeamName,
    string? NextTeamOwner,
    bool DraftCompleted)
{
    public static DraftStateDto Empty { get; } = new(
        null,
        null,
        0,
        0,
        0,
        0,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false);

    public bool HasDraft => DraftId.HasValue;
}

public record AvailablePlayerDto(
    int PlayerId,
    string Name,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age);

public record DraftPickRequestDto(int PlayerId, string Token);
