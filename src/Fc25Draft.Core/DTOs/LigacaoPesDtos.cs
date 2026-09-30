namespace Fc25Draft.Core.DTOs;

/// <summary>Jogador do site com o ID do PES ligado a ele (nulo = sem ligação). Usado pelo Editor PES.</summary>
public record LigacaoPesDto(
    int PlayerId,
    string Name,
    short PositionId,
    int? Age,
    int Overall,
    string? TeamName,
    int? PesId);

/// <summary>Ligação conferida no Editor PES. PesId nulo = desligar (os atributos ficam, só perdem o ID).</summary>
public record DefinirLigacaoPesDto(int PlayerId, int? PesId);

public record ResultadoLigacoesPesDto(int Ligados, int Desligados, int Iguais, int Atualizados = 0);
