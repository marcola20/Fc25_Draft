using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Jogos anotados à mão nas competições anteriores ao contador de jogos.</summary>
public interface IJogosAnotadosService
{
    Task<IReadOnlyList<JogosAnotadosResumoDto>> ListarAsync(CancellationToken ct);

    Task<JogosAnotadosJogadorDto?> DoJogadorAsync(int playerId, CancellationToken ct);

    /// <summary>Grava as linhas do jogador; Jogos nulo apaga a anotação daquele time na competição.</summary>
    Task SalvarAsync(int playerId, IReadOnlyCollection<JogosAnotadosSalvarDto> linhas, CancellationToken ct);

    /// <summary>Times que disputaram alguma competição sem contador.</summary>
    Task<IReadOnlyList<JogosAnotadosTimeDto>> TimesAsync(CancellationToken ct);

    /// <summary>Todos que passaram pelo time nas competições sem contador, com o que já foi anotado.</summary>
    Task<JogosAnotadosTimeGradeDto?> DoTimeAsync(Guid timeId, CancellationToken ct);

    /// <summary>Grava a grade do time; Jogos nulo apaga a anotação daquele jogador na competição (por esse time).</summary>
    Task SalvarDoTimeAsync(Guid timeId, IReadOnlyCollection<JogosAnotadosDoTimeSalvarDto> linhas, CancellationToken ct);
}
