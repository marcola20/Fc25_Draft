using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record TeamCreateDto(string TeamName, string? OwnerName, string? AuxiliarName = null);

public record TeamUpdateDto(string TeamName, string? OwnerName, string? AuxiliarName = null);

public record TeamListItemDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    int Jogadores,
    string? AuxiliarName = null);

public record TeamDetailsDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    string Token,
    int Jogadores,
    string Caixa,
    int QuickSellCount,
    int TransferCount,
    string? AuxiliarName = null,
    string? AuxToken = null,
    int LoanCount = 0);

public record TeamIdentityDto(
    Guid TeamId,
    string TeamName,
    string? Treinador = null,
    string? Papel = null);

public record TeamRosterDto(
    Guid TeamId,
    string TeamName,
    string? OwnerName,
    IReadOnlyList<TeamRosterPlayerDto> Jogadores,
    string? AuxiliarName = null);

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
    int? Escolha,
    decimal? PrecoPedido = null,
    string? EmprestadoPor = null)
{
    /// <summary>Overall em cada posição (id do site 1 = GOL … 13 = SA) pela fórmula do PES; null sem atributos do jogo.</summary>
    public IReadOnlyDictionary<short, int>? OverallPorPosicao { get; init; }

    /// <summary>Número da camisa (aba Camisas do time); nulo = sem número.</summary>
    public short? NumeroCamisa { get; init; }
}

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
    DateTime OccurredAtUtc,
    string? Evolucao = null);
