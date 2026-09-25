namespace Fc25Draft.Core.Entities;

public class Team
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = null!;
    public string? OwnerName { get; set; }
    public string? AuxiliarName { get; set; }
    public decimal Budget { get; set; }
    public decimal BudgetBlocked { get; set; }
    public int QuickSellCount { get; set; }
    public int TransferCount { get; set; }
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Mínimo de elenco temporário só deste time (ex.: perdeu jogadores no draft de expansão).
    /// Nulo usa <see cref="TransferConfig.MinRosterSize"/>.
    /// </summary>
    public int? MinRosterSizeOverride { get; set; }

    /// <summary>
    /// Limite de vendas rápidas só deste time (ex.: time novo que entra com menos).
    /// Nulo usa <see cref="TransferConfig.MaxQuickSellPerWindow"/>.
    /// </summary>
    public int? QuickSellLimitOverride { get; set; }

    /// <summary>
    /// Limite de transferências só deste time.
    /// Nulo usa <see cref="TransferConfig.MaxTransfers"/>.
    /// </summary>
    public int? TransferLimitOverride { get; set; }

    public ICollection<TeamRoster> Roster { get; set; } = new List<TeamRoster>();
    public ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
    public ICollection<MarketBid> MarketBids { get; set; } = new List<MarketBid>();
    public ICollection<MarketItem> LeadingMarketItems { get; set; } = new List<MarketItem>();
    public ICollection<MarketItem> WonMarketItems { get; set; } = new List<MarketItem>();
    public ICollection<TeamLineup> Lineups { get; set; } = new List<TeamLineup>();
}
