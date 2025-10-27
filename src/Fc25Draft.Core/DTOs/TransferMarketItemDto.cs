namespace Fc25Draft.Core.DTOs;

public record TransferMarketItemDto(
    Guid MarketItemId,
    int PlayerId,
    string PlayerName,
    string PositionName,
    int Age,
    int Overall,
    decimal PrecoBase,
    decimal PrecoComprarAgora,
    decimal? LanceAtual,
    string? MaiorLanceTeamName,
    string Status,
    DateTime DataInicioUtc);
