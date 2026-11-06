using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Jogos anotados à mão nas competições anteriores ao contador de jogos.</summary>
public interface IJogosAnotadosService
{
    Task<IReadOnlyList<JogosAnotadosResumoDto>> ListarAsync(CancellationToken ct);

    Task<JogosAnotadosJogadorDto?> DoJogadorAsync(int playerId, CancellationToken ct);

    /// <summary>Grava as linhas do jogador; Jogos nulo apaga a anotação daquele time na competição.</summary>
    Task SalvarAsync(int playerId, IReadOnlyCollection<JogosAnotadosSalvarDto> linhas, CancellationToken ct);
}
