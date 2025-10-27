using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record PlayerCreateDto(string Name, int? Age, int Overall, short PositionId);

public record PlayerUpdateDto(string Name, int? Age, int Overall, short PositionId);

public record PlayerListItemDto(
    int PlayerId,
    string Name,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    string Status,
    string? TeamName);

public record PlayerDetailsDto(
    int PlayerId,
    string Name,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    string Status,
    string? TeamName,
    Guid? TeamId);

public record PlayerExportDto(
    string Nome,
    string Posicao,
    int Overall,
    string Status,
    string? Time);

public record PlayerImportResultDto(int Imported, IReadOnlyList<string> Errors);
