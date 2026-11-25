using System.Text.Json.Serialization;

namespace Fc25Draft.Core.DTOs;

// ── Entrada: JSON gerado pelo extrair_eventos.py (Auto_PES21) ────────────────
// Só os campos usados na importação; o JSON inteiro fica guardado em LigaPartidaImportacao.

public record ResultadoPesRequest(
    [property: JsonPropertyName("video")] string? Video,
    [property: JsonPropertyName("serie")] string? Serie,
    [property: JsonPropertyName("rodada")] int? Rodada,
    [property: JsonPropertyName("casa")] ResultadoPesTime? Casa,
    [property: JsonPropertyName("fora")] ResultadoPesTime? Fora,
    [property: JsonPropertyName("eventos")] IReadOnlyList<ResultadoPesEvento>? Eventos,
    [property: JsonPropertyName("notas")] ResultadoPesNotas? Notas,
    [property: JsonPropertyName("status")] string? Status,
    // Quando o vídeo foi gravado (data do arquivo): identifica o vídeo mesmo com o nome repetido.
    [property: JsonPropertyName("gravado_em")] string? GravadoEm = null,
    // Registro individual de jogo de cada jogador; null quando o vídeo não tem as fotos.
    [property: JsonPropertyName("jogadores")] ResultadoPesJogadores? Jogadores = null);

public record ResultadoPesTime(
    [property: JsonPropertyName("time")] string? Time,
    [property: JsonPropertyName("gols")] int? Gols);

/// <summary>
/// Tipo: gol | gol_contra | cartao_amarelo | cartao_vermelho | substituicao.
/// Lado do gol contra = time que ganha o gol no placar (o autor é do outro lado).
/// </summary>
public record ResultadoPesEvento(
    [property: JsonPropertyName("minuto")] int? Minuto,
    [property: JsonPropertyName("lado")] string? Lado,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("jogador")] string? Jogador,
    [property: JsonPropertyName("assistencia")] string? Assistencia,
    [property: JsonPropertyName("entrou")] string? Entrou,
    [property: JsonPropertyName("saiu")] string? Saiu);

public record ResultadoPesNotas(
    [property: JsonPropertyName("casa")] IReadOnlyList<ResultadoPesNota>? Casa,
    [property: JsonPropertyName("fora")] IReadOnlyList<ResultadoPesNota>? Fora);

public record ResultadoPesNota(
    [property: JsonPropertyName("numero")] int? Numero,
    [property: JsonPropertyName("jogador")] string? Jogador,
    [property: JsonPropertyName("nota")] double? Nota,
    [property: JsonPropertyName("melhor_em_campo")] bool? MelhorEmCampo);

public record ResultadoPesJogadores(
    [property: JsonPropertyName("casa")] IReadOnlyList<ResultadoPesJogador>? Casa,
    [property: JsonPropertyName("fora")] IReadOnlyList<ResultadoPesJogador>? Fora);

/// <summary>
/// Números de um jogador no "Registro individual de jogo" do PES. Qualquer número pode vir nulo (ilegível).
/// Os contadores vêm como double para aceitar "12" e "12.0"; a importação arredonda.
/// </summary>
public record ResultadoPesJogador(
    [property: JsonPropertyName("jogador")] string? Jogador,
    [property: JsonPropertyName("posicao")] string? Posicao,
    // goleiro | defesa | meio | ataque
    [property: JsonPropertyName("grupo")] string? Grupo,
    [property: JsonPropertyName("nota")] double? Nota,
    [property: JsonPropertyName("melhor_em_campo")] bool? MelhorEmCampo,
    [property: JsonPropertyName("minutos")] double? Minutos,
    [property: JsonPropertyName("gols")] double? Gols,
    [property: JsonPropertyName("gols_de_penalti")] double? GolsDePenalti,
    [property: JsonPropertyName("gols_de_falta")] double? GolsDeFalta,
    [property: JsonPropertyName("assistencias")] double? Assistencias,
    [property: JsonPropertyName("chutes")] double? Chutes,
    [property: JsonPropertyName("chutes_a_gol")] double? ChutesAGol,
    [property: JsonPropertyName("passes")] double? Passes,
    [property: JsonPropertyName("passes_certos")] double? PassesCertos,
    [property: JsonPropertyName("cruzamentos")] double? Cruzamentos,
    [property: JsonPropertyName("faltas")] double? Faltas,
    [property: JsonPropertyName("impedimentos")] double? Impedimentos,
    [property: JsonPropertyName("cobrancas_de_falta")] double? CobrancasDeFalta,
    [property: JsonPropertyName("escanteios")] double? Escanteios,
    [property: JsonPropertyName("interceptacoes")] double? Interceptacoes,
    [property: JsonPropertyName("toques")] double? Toques,
    [property: JsonPropertyName("distancia_de_drible")] double? DistanciaDeDrible,
    [property: JsonPropertyName("velocidade_media")] double? VelocidadeMedia,
    [property: JsonPropertyName("desarmes")] double? Desarmes,
    [property: JsonPropertyName("desarmes_certos")] double? DesarmesCertos,
    [property: JsonPropertyName("afastamentos")] double? Afastamentos,
    [property: JsonPropertyName("chutes_sofridos")] double? ChutesSofridos,
    [property: JsonPropertyName("chutes_sofridos_a_gol")] double? ChutesSofridosAGol,
    [property: JsonPropertyName("defesas")] double? Defesas);

// ── Saída ────────────────────────────────────────────────────────────────────

/// <summary>Nome do PES que não casou com segurança com nenhum jogador do elenco.</summary>
public record ResultadoPesNaoIdentificadoDto(
    string Lado,
    string Time,
    string? Nome,
    // gol | gol_contra | assistencia | cartao_amarelo | cartao_vermelho | entrou | saiu | titular
    string Papel,
    int? Minuto,
    string Motivo);

public record ResultadoPesRespostaDto(
    Guid PartidaId,
    string Competicao,
    int Rodada,
    string Casa,
    string Fora,
    int GolsCasa,
    int GolsFora,
    // "criado" na primeira importação da partida, "atualizado" nas seguintes.
    string Situacao,
    bool Simulado,
    string StatusPartida,
    int EventosGravados,
    // true = titulares do retrato vieram das notas do PES; false = escalação ativa do time.
    bool TitularesDasNotas,
    IReadOnlyList<ResultadoPesNaoIdentificadoDto> NaoIdentificados,
    IReadOnlyList<string> Avisos);

/// <summary>Resultado segurado à espera da liberação (/admin/resultados-pendentes).</summary>
public record ResultadoPesPendenteDto(
    Guid Id,
    string? Video,
    string? Competicao,
    string? Onde,
    string Casa,
    string Fora,
    int GolsCasa,
    int GolsFora,
    DateTime? DataHora,
    IReadOnlyList<string> Avisos,
    string? Erro,
    DateTime EnviadoEm);

// ── Vídeo do jogo no YouTube (botão "Atribuir" do painel do Auto_PES21) ─────

/// <summary>
/// O jogo é achado como no envio do resultado: mandante x visitante, com o título do vídeo no
/// YouTube no lugar do nome do arquivo (ele cita a competição e se é jogo extra).
/// </summary>
public record VideoPesRequest(
    [property: JsonPropertyName("video")] string? Video,
    [property: JsonPropertyName("serie")] string? Serie,
    [property: JsonPropertyName("rodada")] int? Rodada,
    [property: JsonPropertyName("casa")] ResultadoPesTime? Casa,
    [property: JsonPropertyName("fora")] ResultadoPesTime? Fora,
    [property: JsonPropertyName("link")] string? Link,
    [property: JsonPropertyName("gravado_em")] string? GravadoEm = null);

public record VideoPesRespostaDto(
    Guid PartidaId,
    string Competicao,
    string Onde,
    string Casa,
    string Fora,
    string VideoId,
    // O vídeo que a partida tinha antes, quando era outro.
    string? VideoAnterior);
