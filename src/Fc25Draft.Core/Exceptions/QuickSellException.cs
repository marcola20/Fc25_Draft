using System;

namespace Fc25Draft.Core.Exceptions;

public class QuickSellException : Exception
{
    public QuickSellException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
