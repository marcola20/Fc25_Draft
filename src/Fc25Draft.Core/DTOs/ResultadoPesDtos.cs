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
    [property: JsonPropertyName("status")] string? Status);

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
