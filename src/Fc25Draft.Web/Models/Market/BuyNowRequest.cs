using System;

namespace Fc25Draft.Web.Models.Market;

public class BuyNowRequest
{
    public Guid ItemId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
