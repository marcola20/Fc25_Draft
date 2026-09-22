namespace Fc25Draft.Core.DTOs;

public record TransferConfigDto(
    int MaxQuickSellPerWindow,
    int MaxTransfers,
    int MinRosterSize);

/// <summary>Contador de vendas rápidas (quick sell) de um time na janela atual.</summary>
/// <param name="LimiteIndividual">Limite só deste time; nulo usa o limite geral da janela.</param>
public record TeamQuickSellStatusDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    int QuickSellCount,
    int? LimiteIndividual = null);

/// <summary>Tamanho do elenco de um time e o mínimo temporário dele, se houver.</summary>
public record TeamElencoMinimoDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    int Elenco,
    int? MinimoTemporario);
