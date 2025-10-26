namespace Fc25Draft.Core.DTOs;

public record TeamCreateDto(string TeamName, string? OwnerName);
public record TeamUpdateDto(string TeamName, string? OwnerName);
