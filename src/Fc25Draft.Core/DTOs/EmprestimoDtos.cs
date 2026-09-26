namespace Fc25Draft.Core.DTOs;

/// <summary>Empréstimo em andamento: quem é o dono, quem está com o jogador e se dá para comprar.</summary>
public record EmprestimoDto(
    Guid EmprestimoId,
    int PlayerId,
    Guid PlayerGuid,
    string JogadorNome,
    string Posicao,
    int Overall,
    Guid DonoTeamId,
    string DonoTeamNome,
    Guid TomadorTeamId,
    string TomadorTeamNome,
    decimal? ValorOpcaoCompra,
    DateTime InicioUtc);
