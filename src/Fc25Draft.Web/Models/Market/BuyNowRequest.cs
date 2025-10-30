using System;

namespace Fc25Draft.Web.Models.Market;

public class BuyNowRequest
{
    public Guid ItemId { get; set; }

    public uint RowVersion { get; set; }

    public string? TeamToken { get; set; }
}
