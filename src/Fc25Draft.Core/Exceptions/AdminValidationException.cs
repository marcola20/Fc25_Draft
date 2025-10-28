namespace Fc25Draft.Core.Exceptions;

public class AdminValidationException : Exception
{
    public AdminValidationException(string message)
        : base(message)
    {
    }
}
