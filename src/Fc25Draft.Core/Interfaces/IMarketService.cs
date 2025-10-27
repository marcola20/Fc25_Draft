using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketService
{
    Task<IReadOnlyList<TransferMarketItem>> GenerateRoundAsync(CancellationToken ct);
    Task<IReadOnlyList<TransferMarketItemDto>> GetOpenItemsAsync(CancellationToken ct);
}
