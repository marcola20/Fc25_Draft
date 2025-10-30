using System;

namespace Fc25Draft.Web.Models.Market;

public class BidRequest
{
    public Guid ItemId { get; set; }
    public decimal Amount { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
