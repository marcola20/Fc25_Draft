using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Um pedido de arte como vai para o Artista CBFV: tudo que ele precisa, já resolvido.</summary>
public record PedidoArteDto(
    Guid Id,
    string Tipo,          // "premio" | "contratacao" | "draft" (1 escolha) | "draft-grupo" (escolhas 4 a 8)
    string Titulo,
    PremioArteDto? Premio,
    ContratacaoArteDto? Contratacao,
    DraftArteDto? Draft = null);

public record PremioArteDto(
    string Sigla,         // "MVP"
    string NomePremio,    // "Melhor Jogador da Temporada"
    int Temporada,
    string Nome,
    string? Clube,
    int? Numero,
    int Votos,
    string? Resumo,       // "nota 7,4 · 18 jogos · 9 G/A"
    bool Treinador,
    string? FotoUrl);     // relativo ao site: "/fotos/jogadores/1453"

public record ContratacaoArteDto(
    bool Emprestimo,
    string Nome,
    string Clube,
    string? Origem,
    decimal? Valor,
    int? Numero,
    int Over,
    string? Posicao,
    string? FotoUrl);

public record DraftArteDto(
    string NomeDraft,     // "Draft — Brasileirão CBFV 2010"
    int? Temporada,
    IReadOnlyList<EscolhaDraftArteDto> Escolhas);

public record EscolhaDraftArteDto(
    int Escolha,          // número geral da escolha (1, 2, 3...)
    int Rodada,
    string Nome,
    string Clube,
    int Over,
    string? Posicao,
    int? Idade,
    int? Numero,
    string? FotoUrl);

/// <summary>O agente avisa o que aconteceu com o pedido.</summary>
public record ConcluirPedidoArteRequest(bool Ok, string? Erro);

public record PedidoArteResumoDto(
    Guid Id,
    TipoPedidoArte Tipo,
    string Titulo,
    SituacaoPedidoArte Situacao,
    int Tentativas,
    string? Erro,
    DateTime CriadoEmUtc,
    DateTime? EntregueEmUtc,
    DateTime? ConcluidoEmUtc);

public record ArtesConfigDto(bool Ligado, decimal ValorMinimo, int OverMinimo, DateTime? TransferenciasDesdeUtc);
