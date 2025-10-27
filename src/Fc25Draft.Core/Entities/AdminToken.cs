using System;

namespace Fc25Draft.Core.Entities;

public class AdminToken
{
    public int AdminTokenId { get; set; }

    public Guid Token { get; set; }
}
