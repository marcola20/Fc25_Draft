namespace Fc25Draft.Core.DTOs;

public record PlayerCreateDto(string Name, int? Age, int Overall, short PositionId);
public record PlayerUpdateDto(string Name, int? Age, int Overall, short PositionId);
