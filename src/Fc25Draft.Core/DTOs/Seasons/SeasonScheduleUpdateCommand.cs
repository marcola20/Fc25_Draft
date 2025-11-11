namespace Fc25Draft.Core.DTOs.Seasons;

public sealed record SeasonScheduleUpdateCommand(Guid SeasonId, IReadOnlyList<SeasonScheduleUpdateItemDto> Items);
