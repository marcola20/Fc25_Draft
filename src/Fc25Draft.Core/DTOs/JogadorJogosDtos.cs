namespace Fc25Draft.Core.DTOs;

/// <summary>Uma partida que o jogador jogou (titular ou entrando), com a nota do PES quando tem.</summary>
public record JogadorJogoDto(
    Guid PartidaId,
    // Horário de Brasília da rodada; sem data marcada, o fim da partida.
    DateTime? Quando,
    string Competicao,
    string Rotulo,
    Guid TimeId,
    string TimeNome,
    Guid AdversarioId,
    string AdversarioNome,
    bool EmCasa,
    int GolsPro,
    int GolsContra,
    // "V", "E" ou "D"; nulo enquanto a partida não terminou.
    string? Resultado,
    // Nulo nas partidas sem escalação gravada (não dá para saber).
    bool? Titular,
    decimal? Nota,
    bool MelhorEmCampo,
    int Gols,
    int Assistencias,
    int Amarelos,
    int Vermelhos);

public record JogadorJogosDto(
    IReadOnlyList<JogadorJogoDto> Jogos,
    int JogosComNota,
    decimal? MediaNotas,
    decimal? MelhorNota,
    int VezesMelhorEmCampo);

/// <summary>Um ponto da evolução do overall: o valor a partir daquela data e o porquê.</summary>
public record JogadorOverallPontoDto(DateTime Data, int Overall, string Motivo);
