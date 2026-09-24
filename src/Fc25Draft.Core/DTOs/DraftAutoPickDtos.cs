using System;
using System.Collections.Generic;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs;

/// <summary>Uma rodada em que o time ainda vai escolher, com a posição marcada (modo posição).</summary>
public record DraftAutoPickRodadaStatusDto(
    int Round,
    // Zero no próximo draft: a ordem só existe quando o draft é gerado.
    int OverallPick,
    int? OverallMin,
    int? OverallMax,
    short? PositionId);

public record DraftAutoPickJogadorDto(
    short? ListaPositionId,
    int Ordem,
    int PlayerId,
    string Name,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    bool Disponivel);

public record DraftAutoPickDto(
    Guid TeamId,
    string TeamName,
    bool Configurado,
    DraftAutoPickModo Modo,
    bool Ativo,
    DateTime? AtualizadoEm,
    IReadOnlyList<DraftAutoPickRodadaStatusDto> MinhasRodadas,
    IReadOnlyList<DraftAutoPickJogadorDto> Jogadores,
    // true = lista para o próximo draft (ainda não criado); as rodadas vêm do planejamento do admin.
    bool ProximoDraft = false);

public record DraftAutoPickRodadaDto(int Round, short PositionId);

public record DraftAutoPickListaPosicaoDto(short PositionId, IReadOnlyList<int> PlayerIds);

public record DraftAutoPickSaveRequestDto(
    DraftAutoPickModo Modo,
    bool Ativo,
    IReadOnlyList<int>? Jogadores,
    IReadOnlyList<DraftAutoPickRodadaDto>? Rodadas,
    IReadOnlyList<DraftAutoPickListaPosicaoDto>? Listas);

/// <summary>Resultado de salvar: se já era a vez do time, as escolhas automáticas feitas em seguida.</summary>
public record DraftAutoPickSaveResultDto(DraftAutoPickDto Config, DraftPickResultDto? Escolhas);

/// <summary>Rodada do próximo draft, planejada pelo admin.</summary>
public record DraftPlanoRodadaDto(int Round, int? OverallMin, int? OverallMax);

/// <summary>Draft gerado e, se já começou com escolhas automáticas, a sequência feita.</summary>
public record GenerateDraftResultDto(DraftStateDto State, DraftPickResultDto? Escolhas);
