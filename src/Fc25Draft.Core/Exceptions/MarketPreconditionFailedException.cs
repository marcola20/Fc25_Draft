namespace Fc25Draft.Core.Exceptions;

public class MarketPreconditionFailedException : Exception
{
    public MarketPreconditionFailedException(string message)
        : base(message)
    {
    }
}
