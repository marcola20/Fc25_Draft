namespace Fc25Draft.Core.Exceptions;

public class MarketItemValidationException : Exception
{
    public MarketItemValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
