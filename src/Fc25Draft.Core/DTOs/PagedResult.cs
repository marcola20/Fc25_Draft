using System;

namespace Fc25Draft.Core.DTOs;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public static PagedResult<T> Empty(int page = 1, int pageSize = 0)
        => new(Array.Empty<T>(), 0, page, pageSize);

    public int TotalPages =>
        PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 1;

    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

