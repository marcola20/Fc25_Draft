using System;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

public record class TransfersFilter
{
    public Guid? TeamId { get; init; }
    public int? PlayerId { get; init; }
    public TransferType? Type { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Query { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
