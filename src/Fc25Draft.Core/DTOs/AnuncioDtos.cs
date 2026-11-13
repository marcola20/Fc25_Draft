namespace Fc25Draft.Core.DTOs;

public record AnuncioDto(
    Guid AnuncioId,
    string Grupo,
    string Titulo,
    string Texto,
    string Explicacao,
    string Resumo,
    string TextoSite,
    int Ordem,
    DateTime? EnviadoEm,
    DateTime? PublicadoEm,
    DateTime AtualizadoEm);

public record AnuncioSalvarRequest(
    string Grupo,
    string Titulo,
    string Texto,
    string Explicacao,
    int Ordem,
    string Resumo = "",
    string TextoSite = "");

/// <summary>Anúncio publicado, como os técnicos veem na página de novidades.</summary>
public record AnuncioPublicadoDto(Guid AnuncioId, string Titulo, string Resumo, string TextoSite, DateTime PublicadoEm);
