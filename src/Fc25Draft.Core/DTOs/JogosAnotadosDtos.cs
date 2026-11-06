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
