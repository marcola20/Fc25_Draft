using System;
using System.Net;

namespace Fc25Draft.Web.Services
{
    public class MarketConcurrencyException : Exception
    {
        public MarketConcurrencyException(string message, HttpStatusCode statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }
    }
}
