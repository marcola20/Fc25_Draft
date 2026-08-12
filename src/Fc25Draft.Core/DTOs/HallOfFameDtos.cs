using Fc25Draft.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Core.DTOs;

public record HallOfFameEntryDto(
    Guid Id,
    string Descricao,
    TipoCompetition Tipo,
    string TimeCampeao,
    string? Tecnico,
    int? Ano,
    string? Temporada,
    DateTime CriadoEm,
    DateTime AtualizadoEm);

public record HallOfFameCreateRequest(
    [Required, MaxLength(200)] string Descricao,
    TipoCompetition Tipo,
    [Required, MaxLength(120)] string TimeCampeao,
    [MaxLength(120)] string? Tecnico = null,
    int? Ano = null,
    [MaxLength(60)] string? Temporada = null);

public record HallOfFameUpdateRequest(
    [Required, MaxLength(200)] string Descricao,
    TipoCompetition Tipo,
    [Required, MaxLength(120)] string TimeCampeao,
    [MaxLength(120)] string? Tecnico = null,
    int? Ano = null,
    [MaxLength(60)] string? Temporada = null);
