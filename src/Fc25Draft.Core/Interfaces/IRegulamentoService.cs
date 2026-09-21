using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Regulamento por temporada: cada ano tem a sua versão, criada a partir da anterior.</summary>
public interface IRegulamentoService
{
    Task<IReadOnlyList<RegulamentoDto>> ListAsync(CancellationToken ct);

    Task<RegulamentoDto?> GetAsync(Guid regulamentoId, CancellationToken ct);

    Task<RegulamentoDto?> GetPorTemporadaAsync(int temporada, CancellationToken ct);

    /// <summary>Regulamento da temporada mais recente cadastrada.</summary>
    Task<RegulamentoDto?> GetAtualAsync(CancellationToken ct);

    Task<RegulamentoDto> CriarAsync(RegulamentoCriarRequest request, CancellationToken ct);

    Task<RegulamentoDto> SalvarAsync(Guid regulamentoId, string titulo, string conteudo, CancellationToken ct);

    Task ExcluirAsync(Guid regulamentoId, CancellationToken ct);
}
