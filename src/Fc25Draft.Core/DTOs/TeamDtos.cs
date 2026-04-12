using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record TeamCreateDto(string TeamName, string? OwnerName);

public record TeamUpdateDto(string TeamName, string? OwnerName);

public record TeamListItemDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    int Jogadores);

public record TeamDetailsDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    string Token,
    int Jogadores,
    string Caixa,
    int QuickSellCount,
    int TransferCount);

public record TeamIdentityDto(
    Guid TeamId,
    string TeamName);

public record TeamRosterDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    IReadOnlyList<TeamRosterPlayerDto> Jogadores);

public record TeamRosterPlayerDto(
    Guid PlayerGuid,
    int PlayerId,
    short PositionId,
    string Nome,
    string Posicao,
    int Overall,
    int? Idade,
    DateTime? EscolhidoEm,
    int? Rodada,
    int? Escolha);

public record TeamExportDto(
    string Time,
    string? Responsavel,
    int TotalJogadores);

public record TeamBudgetDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    decimal Budget);

public record QuickSellResultDto(
    Guid TeamId,
    Guid PlayerId,
    int OldOverall,
    int NewOverall,
    decimal BasePrice,
    decimal Payout,
    decimal TeamBudgetAfter,
    DateTime OccurredAtUtc);
