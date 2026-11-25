using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Um pedido de arte como vai para o Artista CBFV: tudo que ele precisa, já resolvido.</summary>
public record PedidoArteDto(
    Guid Id,
    string Tipo,          // "premio" | "contratacao" | "draft" (1 escolha) | "draft-grupo" (escolhas 4 a 8) | "selecao"
    string Titulo,
    PremioArteDto? Premio,
    ContratacaoArteDto? Contratacao,
    DraftArteDto? Draft = null,
    SelecaoArteDto? Selecao = null,
    AposentadoriaArteDto? Aposentadoria = null,
    HatTrickArteDto? HatTrick = null,
    CampeaoArteDto? Campeao = null,
    AcessoArteDto? Acesso = null,
    ArtilhariaArteDto? Artilharia = null);

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

public record SelecaoArteDto(
    string Competicao,    // "Brasileirão — Série A 2010"
    int Rodada,
    int? Temporada,
    IReadOnlyList<JogadorSelecaoDto> Jogadores);   // 11, na ordem das vagas do 4-3-3

public record JogadorSelecaoDto(
    string Vaga,          // GOL, LD, ZAG, ZAG, LE, MEI, MEI, MEI, PE, CA, PD
    string Nome,
    string Clube,
    decimal Nota,
    int Gols,
    int Assistencias,
    bool Craque,          // a maior nota da seleção
    int? Numero,
    string? FotoUrl);

/// <param name="LendaDe">Clubes de que ele era ídolo: vira lenda deles (arte especial). Vazio = aposentadoria comum.</param>
/// <param name="CamisaAposentada">Número aposentado no <paramref name="Clube"/> (ele era ídolo de lá e usava esse número).</param>
public record AposentadoriaArteDto(
    string Nome, string? Clube, int? Idade, int Temporadas, int Jogos, int Gols, int Assistencias,
    IReadOnlyList<string> Clubes, IReadOnlyList<string> Titulos, string? FotoUrl, int Temporada,
    IReadOnlyList<string>? LendaDe = null, short? CamisaAposentada = null);

public record HatTrickArteDto(
    string Nome, string Clube, int Gols, IReadOnlyList<int?> Minutos, string Casa, string Fora, int GolsCasa,
    int GolsFora, string Competicao, int? Numero, string? FotoUrl, int? Temporada);

public record JogadorCampeaoDto(string Nome, int? Numero, string? FotoUrl);
public record CampanhaDto(int Vitorias, int Empates, int Derrotas, int GolsPro, int GolsContra);
public record CampeaoArteDto(
    string Competicao, int? Temporada, string Clube, IReadOnlyList<JogadorCampeaoDto> Jogadores, CampanhaDto? Campanha,
    string? Destaque);

public record TimeMovimentoDto(string Clube, string? Detalhe);
public record AcessoArteDto(int Temporada, IReadOnlyList<TimeMovimentoDto> Sobem, IReadOnlyList<TimeMovimentoDto> Caem);

public record DestaqueEstatDto(int Posicao, string Nome, string? Clube, int Valor, int? Numero, string? FotoUrl);
public record ArtilhariaArteDto(
    string Competicao, int? Temporada, IReadOnlyList<DestaqueEstatDto> Artilheiros, IReadOnlyList<DestaqueEstatDto> Garcons);

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
