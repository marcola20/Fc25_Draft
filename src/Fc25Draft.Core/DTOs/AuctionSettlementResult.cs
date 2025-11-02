namespace Fc25Draft.Core.DTOs;

public sealed record AuctionSettlementResult(int Sold, int Expired, int Canceled)
{
    public int Total => Sold + Expired + Canceled;
}
