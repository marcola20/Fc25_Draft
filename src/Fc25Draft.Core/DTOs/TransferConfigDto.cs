namespace Fc25Draft.Core.DTOs;

public record TransferConfigDto(
    int MaxQuickSellPerWindow,
    int MaxTransfers,
    int MinRosterSize);

/// <summary>Contador de vendas rápidas (quick sell) de um time na janela atual.</summary>
public record TeamQuickSellStatusDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    int QuickSellCount);
