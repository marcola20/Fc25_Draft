namespace Fc25Draft.Core.Exceptions;

public class MarketConflictException : Exception
{
    public MarketConflictException(string message) : base(message)
    {
    }

    public MarketConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
