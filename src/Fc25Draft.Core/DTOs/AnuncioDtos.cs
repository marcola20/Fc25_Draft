namespace Fc25Draft.Core.DTOs;

public record AnuncioDto(
    Guid AnuncioId,
    string Grupo,
    string Titulo,
    string Texto,
    string Explicacao,
    int Ordem,
    DateTime? EnviadoEm,
    DateTime AtualizadoEm);

public record AnuncioSalvarRequest(string Grupo, string Titulo, string Texto, string Explicacao, int Ordem);
