namespace Fc25Draft.Core.DTOs.Seasons;

public sealed record CompetitionUpsertCommand(string Name, int Order, bool IsActive);
