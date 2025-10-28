namespace Fc25Draft.Core.Exceptions;

public class AdminConflictException : Exception
{
    public AdminConflictException(string message)
        : base(message)
    {
    }
}
