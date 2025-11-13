using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionDetailsDto(
    CompetitionSummaryDto Competition,
    IReadOnlyList<CompetitionTeamDto> Teams,
    IReadOnlyList<CompetitionRoundDto> Rounds,
    IReadOnlyList<CompetitionStandingDto> Standings,
    IReadOnlyList<CompetitionTeamStatDto> TeamStats,
    IReadOnlyList<CompetitionPlayerStatDto> PlayerStats);
