using System;
using System.Net;

namespace Fc25Draft.Web.Services;

public sealed class QuickSellException : Exception
{
    public QuickSellException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
