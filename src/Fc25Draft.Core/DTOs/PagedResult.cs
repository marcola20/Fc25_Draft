namespace Fc25Draft.Core.DTOs;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total);
