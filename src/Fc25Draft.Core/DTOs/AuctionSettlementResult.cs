namespace Fc25Draft.Core.DTOs;

public sealed record AuctionSettlementResult(int Sold, int Expired)
{
    public int Total => Sold + Expired;
}
