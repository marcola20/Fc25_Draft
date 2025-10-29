using System;

namespace Fc25Draft.Core.Exceptions;

public class MarketSyncException : Exception
{
    public MarketSyncException(string message) : base(message)
    {
    }
}
