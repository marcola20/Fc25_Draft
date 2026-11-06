namespace Fc25Draft.Core.DTOs;

/// <summary>Um time do jogador numa competição sem contador. Jogos nulo = ainda não anotado.</summary>
public record JogosAnotadosLinhaDto(Guid TimeId, string TimeNome, int? Jogos);

public record JogosAnotadosTimeDto(Guid TimeId, string TimeNome);

public record JogosAnotadosCompeticaoDto(
    Guid LigaId,
    string Nome,
    int? Temporada,
    // Times em que ele esteve nessa competição (pela carreira) e os já anotados.
    IReadOnlyList<JogosAnotadosLinhaDto> Linhas,
    // Todos os times que disputaram a competição, para anotar um time que a carreira não achou.
    IReadOnlyList<JogosAnotadosTimeDto> Participantes,
    // Gols e assistências registrados ali (ajudam a conferir a anotação).
    int Gols,
    int Assistencias);

public record JogosAnotadosJogadorDto(
    int PlayerId,
    string Nome,
    string Posicao,
    string? TimeAtual,
    IReadOnlyList<JogosAnotadosCompeticaoDto> Competicoes);

/// <summary>Jogador que já tem jogos anotados (para acompanhar quem já foi preenchido).</summary>
public record JogosAnotadosResumoDto(int PlayerId, string Nome, int Jogos, int Competicoes, DateTime AtualizadoEmUtc);

public record JogosAnotadosSalvarDto(Guid LigaId, Guid TimeId, int? Jogos);

// ---- Por time: todos que passaram pelo time nas competições sem contador ----

/// <summary>Competição sem contador que o time disputou; JogosDoTime = jogos encerrados dele ali (o teto da anotação).</summary>
public record JogosAnotadosColunaDto(Guid LigaId, string Nome, int? Temporada, int JogosDoTime);

/// <summary>Uma competição na linha do jogador. NoPeriodo = estava no elenco (ou marcou) pelo time enquanto ela era jogada.</summary>
public record JogosAnotadosCelulaDto(Guid LigaId, bool NoPeriodo, int? Jogos, int Gols, int Assistencias);

public record JogosAnotadosJogadorDoTimeDto(
    int PlayerId,
    string Nome,
    short PositionId,
    string Posicao,
    IReadOnlyList<JogosAnotadosCelulaDto> Celulas);

public record JogosAnotadosTimeGradeDto(
    Guid TimeId,
    string TimeNome,
    IReadOnlyList<JogosAnotadosColunaDto> Competicoes,
    IReadOnlyList<JogosAnotadosJogadorDoTimeDto> Jogadores);

public record JogosAnotadosDoTimeSalvarDto(int PlayerId, Guid LigaId, int? Jogos);
