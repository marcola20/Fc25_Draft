namespace Fc25Draft.Core.Entities;

public class IdempotencyKey
{
    public required string Key { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
