namespace Fc25Draft.Core.DTOs;

public record TransferConfigDto(
    int MaxQuickSellPerWindow,
    int MaxTransfers,
    int MinRosterSize);
