namespace Fc25Draft.Core.DTOs;

public record NegotiationListDto(
    Guid NegotiationId,
    string Tipo,
    string Status,
    string OrigemTeamName,
    string DestinoTeamName,
    decimal? ValorOferecido,
    DateTime DataInicioUtc,
    DateTime? DataFechamentoUtc,
    string? Observacao,
    IReadOnlyList<string> JogadoresOrigem,
    IReadOnlyList<string> JogadoresDestino);

public record NegotiationCreateDto(
    string TokenOrigem,
    string TokenDestino,
    string Tipo,
    decimal? ValorOferecido,
    IReadOnlyList<int> JogadoresOrigem,
    IReadOnlyList<int> JogadoresDestino,
    string? Observacao);

public record NegotiationResponseDto(string Token, string Acao);

public record NegotiationCancelDto(string Token);
