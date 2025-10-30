using System;

namespace Fc25Draft.Web.Models.Market;

public class BidRequest
{
    public Guid TeamId { get; set; }
    public decimal Amount { get; set; }
}
