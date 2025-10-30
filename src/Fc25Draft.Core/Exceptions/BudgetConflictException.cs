namespace Fc25Draft.Core.Exceptions;

public class BudgetConflictException : Exception
{
    public BudgetConflictException(string message)
        : base(message)
    {
    }

    public BudgetConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
