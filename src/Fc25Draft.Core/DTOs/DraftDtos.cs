using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record DraftStateDto(
    Guid? DraftId,
    string? DraftName,
    int TotalTeams,
    int TotalRounds,
    int TotalPicks,
    int CompletedPicks,
    int? CurrentRound,
    int? CurrentPickInRound,
    int? CurrentOverallPick,
    Guid? CurrentTeamId,
    string? CurrentTeamName,
    string? CurrentTeamOwner,
    Guid? NextTeamId,
    string? NextTeamName,
    string? NextTeamOwner,
    bool DraftCompleted,
    bool IsExpansao = false,
    bool AguardandoProtecao = false,
    int? TempoPorEscolhaMinutos = null,
    // Quando acaba o tempo da vez atual (nulo = sem limite ou relógio ainda não começou).
    DateTime? PrazoUtc = null,
    bool Pausado = false,
    // Pausado: o tempo que falta, congelado (a tela não desconta nada enquanto isso).
    TimeSpan? RestanteNaPausa = null)
{
    public static DraftStateDto Empty { get; } = new(
        null,
        null,
        0,
        0,
        0,
        0,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false);

    public bool HasDraft => DraftId.HasValue;
}

public record AvailablePlayerDto(
    int PlayerId,
    string Name,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    string? TeamName = null);

public record DraftPickRequestDto(int PlayerId, string Token);

public record DraftRoundRuleDto(int Round, int? OverallMin, int? OverallMax);

public record GenerateDraftRequestDto(
    int TotalRounds,
    bool Snake = false,
    IReadOnlyList<DraftRoundRuleDto>? RoundRules = null,
    string? Name = null,
    int? TempoPorEscolhaMinutos = null);

/// <summary>
/// <see cref="Selection"/> é a primeira escolha da sequência; a mensagem dela já junta as
/// escolhas automáticas que vieram em seguida (todas em <see cref="Escolhas"/>).
/// </summary>
public record DraftPickResultDto(
    DraftStateDto State,
    DraftPickSelectionDto? Selection,
    IReadOnlyList<DraftEscolhaResumoDto>? Escolhas = null);

public record DraftEscolhaResumoDto(
    int Round,
    int PickInRound,
    int OverallPick,
    string TeamName,
    string PlayerName,
    string PositionName,
    bool Automatica,
    bool TempoEsgotado = false);

public record DraftPickSelectionDto(
    Guid DraftId,
    int Round,
    int PickInRound,
    int OverallPick,
    Guid TeamId,
    string TeamName,
    string? TeamOwner,
    int PlayerId,
    string PlayerName,
    short PositionId,
    string PositionName,
    string Mensagem,
    string ShareUrl,
    string? NextTeamName,
    string? WhatsappGroupLink);

public record DraftBoardEntryDto(
    Guid DraftId,
    int Round,
    int PickInRound,
    int OverallPick,
    Guid TeamId,
    string TeamName,
    string? TeamOwner,
    int? PlayerId,
    string? PlayerName,
    short? PositionId,
    string? PositionName,
    DateTime? PickedAtUtc,
    bool Automatica = false);

public record DraftBoardExportDto(
    int Rodada,
    int Escolha,
    string Time,
    string? Responsavel,
    string Jogador,
    string Posicao,
    string DataHoraUtc);

public record DraftSummaryDto(
    Guid DraftId,
    string Name,
    int TotalRounds,
    int TotalTeams,
    DateTime CreatedAtUtc);

public record DraftRoundPickDto(
    int PickInRound,
    int OverallPick,
    Guid TeamId,
    string TeamName,
    string? TeamOwner,
    int? PlayerId,
    string? PlayerName,
    DateTime? PickedAtUtc);

public record DraftRoundDetailsDto(
    int RoundNumber,
    int? OverallMin,
    int? OverallMax,
    IReadOnlyList<DraftRoundPickDto> Picks);

public record DraftDetailsDto(
    Guid DraftId,
    string Name,
    int TotalRounds,
    int TotalTeams,
    DateTime CreatedAtUtc,
    IReadOnlyList<DraftRoundDetailsDto> Rounds);

public record DraftRoundCreateDto(int? OverallMin, int? OverallMax);

public record DraftPickOwnerUpdateDto(Guid OwnerTeamId);

public record DraftPickSwapRequestDto(int DraftPickIdA, int DraftPickIdB);

public record DraftPickMoveRequestDto(int DraftPickId, int TargetOverall);

/// <summary>O que o telão do draft mostra.</summary>
public record DraftTelaoDto(
    DraftStateDto State,
    IReadOnlyList<DraftTelaoPickDto> Proximos,
    IReadOnlyList<DraftTelaoPickDto> Ultimas,
    IReadOnlyList<DraftTelaoPickDto> RodadaAtual);

public record DraftTelaoPickDto(
    int Round,
    int PickInRound,
    int OverallPick,
    Guid TeamId,
    string TeamName,
    string? TeamOwner,
    int? PlayerId,
    string? PlayerName,
    string? PositionName,
    int? Overall,
    bool Automatica,
    bool TempoEsgotado,
    DateTime? PickedAtUtc);
