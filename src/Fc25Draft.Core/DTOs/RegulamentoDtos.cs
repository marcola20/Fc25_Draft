namespace Fc25Draft.Core.DTOs;

public record RegulamentoDto(Guid RegulamentoId, int Temporada, string Titulo, string Conteudo, DateTime AtualizadoEm);

/// <summary>Cria o regulamento de uma temporada, opcionalmente copiando o texto de outra.</summary>
public record RegulamentoCriarRequest(int Temporada, string? Titulo, Guid? CopiarDe);
