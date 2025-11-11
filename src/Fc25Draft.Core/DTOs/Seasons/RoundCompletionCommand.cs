namespace Fc25Draft.Core.DTOs.Seasons;

public sealed record RoundCompletionCommand(bool IsCompleted, DateTime? PlayedAtUtc);
