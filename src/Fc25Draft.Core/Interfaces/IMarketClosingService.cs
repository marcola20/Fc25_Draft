using System;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketClosingService
{
    Task<MarketClosePreviewDto> PreviewCloseAsync(CancellationToken ct);
    Task<MarketCloseResultDto> CloseRoundAsync(CancellationToken ct);
    Task<ItemCloseResultDto> CloseItemAsync(Guid marketItemId, CancellationToken ct);
}
