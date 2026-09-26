using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>O resumo da rodada pronto para colar no grupo.</summary>
public interface IResumoRodadaService
{
    /// <summary>As rodadas que já têm resultado, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<ResumoRodadaOpcaoDto>> RodadasAsync(int quantas, CancellationToken ct);

    Task<ResumoDaRodadaDto?> MontarAsync(Guid rodadaId, CancellationToken ct);
}
