namespace Fc25Draft.Core.Exceptions;

public class AdminForbiddenException : Exception
{
    public AdminForbiddenException(string message)
        : base(message)
    {
    }
}
