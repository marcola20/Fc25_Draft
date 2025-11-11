namespace Fc25Draft.Core.DTOs.Seasons;

public sealed record RoundSelectionDto(Guid RoundId, IReadOnlyList<RoundSelectionPlayerDto> Players);

public sealed record RoundSelectionPlayerDto(Guid PlayerId, string PlayerName, string PositionName, int PositionOrder);
