using Fc25Draft.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Core.DTOs;

public record HallOfFameEntryDto(
    Guid Id,
    string Descricao,
    TipoCompetition Tipo,
    Divisao? Divisao,
    string TimeCampeao,
    string? Tecnico,
    Guid? TreinadorId,
    int? Ano,
    string? Temporada,
    DateTime CriadoEm,
    DateTime AtualizadoEm,
    bool Automatica = false,
    string? TimeVice = null,
    string? TecnicoVice = null,
    Guid? TreinadorViceId = null);

public record HallOfFameCreateRequest(
    [Required, MaxLength(200)] string Descricao,
    TipoCompetition Tipo,
    [Required, MaxLength(120)] string TimeCampeao,
    [MaxLength(120)] string? Tecnico = null,
    Guid? TreinadorId = null,
    int? Ano = null,
    [MaxLength(60)] string? Temporada = null,
    Divisao? Divisao = null,
    [MaxLength(120)] string? TimeVice = null,
    [MaxLength(120)] string? TecnicoVice = null,
    Guid? TreinadorViceId = null);

public record HallOfFameUpdateRequest(
    [Required, MaxLength(200)] string Descricao,
    TipoCompetition Tipo,
    [Required, MaxLength(120)] string TimeCampeao,
    [MaxLength(120)] string? Tecnico = null,
    Guid? TreinadorId = null,
    int? Ano = null,
    [MaxLength(60)] string? Temporada = null,
    Divisao? Divisao = null,
    [MaxLength(120)] string? TimeVice = null,
    [MaxLength(120)] string? TecnicoVice = null,
    Guid? TreinadorViceId = null);

/// <summary>Jogador no Hall da Fama: a turma (temporada), a colocação nela e o que fez na liga.</summary>
public record HallDaFamaJogadorDto(
    Guid Id,
    int PlayerId,
    string Nome,
    string Posicao,
    int Overall,
    int Temporada,
    int Colocacao,
    string? Motivo,
    bool Aposentado,
    string? Clube,
    int Jogos,
    int Gols,
    int Assistencias);

public record HallDaFamaJogadorRequest(
    int PlayerId,
    int Temporada,
    [Range(1, 3)] int Colocacao,
    [MaxLength(300)] string? Motivo = null);

/// <summary>Uma final na conta do treinador: a competição, quando foi e por qual clube.</summary>
public record FinalDoTreinadorDto(string Competicao, string Quando, string Time);

/// <summary>Treinador no ranking do Hall da Fama: títulos e vices, com o detalhe de cada um.</summary>
public record TreinadorNoHallDto(
    Guid? TreinadorId,
    string Nome,
    IReadOnlyList<FinalDoTreinadorDto> Titulos,
    IReadOnlyList<FinalDoTreinadorDto> Vices);
