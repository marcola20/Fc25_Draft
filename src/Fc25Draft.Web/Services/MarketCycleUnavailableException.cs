using System;
using System.Net;

namespace Fc25Draft.Web.Services;

public sealed class MarketCycleUnavailableException : Exception
{
    public MarketCycleUnavailableException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
