using System;

namespace Fc25Draft.Core.Entities;

public class DraftWishlistEntry
{
    public Guid DraftWishlistEntryId { get; set; }
    public Guid TeamId { get; set; }
    public int PlayerId { get; set; }
    public int Ordem { get; set; }
    public DateTime CriadoEm { get; set; }

    public Team Team { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
