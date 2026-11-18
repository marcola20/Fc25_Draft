namespace Fc25Draft.Core.DTOs;

/// <summary>Número da camisa de um jogador do elenco (nulo = sem número).</summary>
public record CamisaRequest(int PlayerId, short? Numero);

public record CamisaJogadorDto(
    int PlayerId,
    string Nome,
    short PositionId,
    int Overall,
    short? Numero,
    string? EmprestadoPor);

public record CamisasDoTimeDto(Guid TeamId, IReadOnlyList<CamisaJogadorDto> Jogadores);

/// <summary>Resultado de "Enviar camisas ao site" no Editor PES.</summary>
public record CamisasDoPesResultadoDto(int Gravados, int JaTinham, int Iguais, IReadOnlyList<string> Conflitos);
