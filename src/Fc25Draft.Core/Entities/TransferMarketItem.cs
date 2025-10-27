using System.Collections.ObjectModel;

namespace Fc25Draft.Core.Entities;

public class TransferMarketItem
{
    public Guid MarketItemId { get; set; }

    public int PlayerId { get; set; }

    public decimal PrecoBase { get; set; }

    public decimal? LanceAtual { get; set; }

    public Guid? MaiorLanceTeamId { get; set; }

    public decimal PrecoComprarAgora { get; set; }

    public DateTime DataInicioUtc { get; set; }

    public DateTime? DataFimUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid? VencedorTeamId { get; set; }

    public Player Player { get; set; } = null!;

    public Team? MaiorLanceTeam { get; set; }

    public Team? VencedorTeam { get; set; }

    private readonly ICollection<Bid> _bids = new Collection<Bid>();

    public ICollection<Bid> Bids => _bids;
}
