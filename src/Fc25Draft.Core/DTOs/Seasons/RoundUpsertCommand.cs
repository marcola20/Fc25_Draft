namespace Fc25Draft.Core.DTOs.Seasons;

public sealed record RoundUpsertCommand(string Name, bool IsCompleted, DateTime? PlayedAtUtc, string? Notes);
