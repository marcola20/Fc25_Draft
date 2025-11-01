using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

public record MarketCycleDto
{
    public MarketCycleDto(
        Guid cycleId,
        string name,
        MarketCycleStatus status,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do ciclo é obrigatório.", nameof(name));

        var normalizedStart = EnsureUtc(startsAtUtc);
        var normalizedEnd = EnsureUtc(endsAtUtc);
        var normalizedCreated = EnsureUtc(createdAtUtc);
        var normalizedUpdated = EnsureUtc(updatedAtUtc);

        if (normalizedStart >= normalizedEnd)
            throw new ArgumentException("A data de início deve ser anterior à data de término.", nameof(endsAtUtc));

        CycleId = cycleId;
        Name = name;
        Status = status;
        StartsAtUtc = normalizedStart;
        EndsAtUtc = normalizedEnd;
        CreatedAtUtc = normalizedCreated;
        UpdatedAtUtc = normalizedUpdated;
        Notes = notes;
    }

    public Guid CycleId { get; init; }

    public string Name { get; init; }

    public MarketCycleStatus Status { get; init; }

    public DateTime StartsAtUtc { get; init; }

    public DateTime EndsAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public string? Notes { get; init; }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}

public record MarketItemDto(
    Guid ItemId,
    Guid CycleId,
    int PlayerId,
    string PlayerName,
    string Position,
    int Ovr,
    int Age,
    decimal BasePrice,
    decimal? BuyNowPrice,
    decimal MinIncrement,
    decimal RequiredMinBid,
    DateTime ExpiresAtUtc,
    string Status,
    decimal? CurrentLeaderAmount,
    string? CurrentLeaderTeamName,
    Guid? CurrentLeaderTeamId,
    uint RowVersion);

public record BuyNowResultDto(bool Ok, string Message);
