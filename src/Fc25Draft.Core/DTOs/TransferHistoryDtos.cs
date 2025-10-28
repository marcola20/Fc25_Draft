using System;

namespace Fc25Draft.Core.DTOs;

public record TransferHistoryItemDto(
    DateTime DataUtc,
    int PlayerId,
    string PlayerName,
    string Tipo,
    string? OrigemTeam,
    string? DestinoTeam,
    decimal Valor);

public record TransferHistoryDto(
    Guid TransferId,
    DateTime DataUtc,
    Guid PlayerId,
    string PlayerName,
    string Tipo,
    string? OrigemTeam,
    string? DestinoTeam,
    decimal? Valor,
    string? Notas);
