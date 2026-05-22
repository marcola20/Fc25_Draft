namespace Fc25Draft.Core.Entities;

public class AdminToken
{
    public Guid AdminTokenId { get; set; }
    public string Token { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
}
