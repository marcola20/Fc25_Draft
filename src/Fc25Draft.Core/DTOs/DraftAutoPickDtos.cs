using System;
using System.Collections.Generic;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs;

/// <summary>Uma rodada em que o time ainda vai escolher, com a posição marcada (modo posição).</summary>
public record DraftAutoPickRodadaStatusDto(
    int Round,
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
    IReadOnlyList<DraftAutoPickJogadorDto> Jogadores);

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
