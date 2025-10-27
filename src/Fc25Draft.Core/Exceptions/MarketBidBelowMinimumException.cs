namespace Fc25Draft.Core.Exceptions;

public class MarketBidBelowMinimumException : Exception
{
    public MarketBidBelowMinimumException(string message) : base(message)
    {
    }
}
