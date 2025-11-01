using System;
using System.Net;

namespace Fc25Draft.Web.Services;

public sealed class MarketClientException : Exception
{
    public MarketClientException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
