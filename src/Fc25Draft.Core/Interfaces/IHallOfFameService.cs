using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IHallOfFameService
{
    Task<IReadOnlyList<HallOfFameEntryDto>> GetAllAsync(CancellationToken ct);
    Task<HallOfFameEntryDto> CreateAsync(HallOfFameCreateRequest request, CancellationToken ct);
    Task<HallOfFameEntryDto> UpdateAsync(Guid id, HallOfFameUpdateRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Preenche o vice dos títulos automáticos que ainda estão sem. Devolve quantos preencheu.</summary>
    Task<int> PreencherVicesAsync(CancellationToken ct);

    /// <summary>Títulos e vices de cada treinador, a partir dos registros do Hall da Fama.</summary>
    Task<IReadOnlyList<TreinadorNoHallDto>> TreinadoresAsync(CancellationToken ct);

    Task<IReadOnlyList<HallDaFamaJogadorDto>> JogadoresAsync(CancellationToken ct);
    Task IncluirJogadorAsync(HallDaFamaJogadorRequest request, CancellationToken ct);
    Task RemoverJogadorAsync(Guid id, CancellationToken ct);

    /// <summary>Última temporada cadastrada (para sugerir a turma no admin).</summary>
    Task<int?> UltimaTemporadaAsync(CancellationToken ct);
}
