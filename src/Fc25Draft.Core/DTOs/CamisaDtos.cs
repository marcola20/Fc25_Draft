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

/// <summary>Número aposentado no clube, em homenagem a uma lenda (ninguém mais usa).</summary>
public record CamisaAposentadaDto(short Numero, int JogadorId, string Nome);

public record CamisasDoTimeDto(
    Guid TeamId, IReadOnlyList<CamisaJogadorDto> Jogadores, IReadOnlyList<CamisaAposentadaDto>? Aposentadas = null);

/// <summary>Resultado de "Enviar camisas ao site" no Editor PES.</summary>
public record CamisasDoPesResultadoDto(int Gravados, int JaTinham, int Iguais, IReadOnlyList<string> Conflitos);
