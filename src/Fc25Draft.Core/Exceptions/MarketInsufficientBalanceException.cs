namespace Fc25Draft.Core.Exceptions;

public class MarketInsufficientBalanceException : Exception
{
    public MarketInsufficientBalanceException(string message) : base(message)
    {
    }
}
