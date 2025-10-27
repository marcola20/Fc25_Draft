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
    bool DraftCompleted)
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
    int? Age);

public record DraftPickRequestDto(int PlayerId, string Token);

public record DraftRoundRuleDto(int Round, int? OverallMin, int? OverallMax);

public record GenerateDraftRequestDto(int TotalRounds, bool Snake = false, IReadOnlyList<DraftRoundRuleDto>? RoundRules = null);

public record DraftPickResultDto(
    DraftStateDto State,
    DraftPickSelectionDto? Selection);

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
    DateTime? PickedAtUtc);

public record DraftBoardExportDto(
    int Rodada,
    int Escolha,
    string Time,
    string? Responsavel,
    string Jogador,
    string Posicao,
    string DataHoraUtc);
