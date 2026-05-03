using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ILigaPublicService
{
    Task<LigaDto?> GetAtualAsync(CancellationToken ct);
    Task<LigaDto?> GetByIdAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaClassificacaoItemDto>> GetClassificacaoAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaArtilheiroDto>> GetArtilheirosAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaArtilheiroDto>> GetAssistenciasAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaCartaoEstatDto>> GetCartoesEstatAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaKnockoutJogoDto>> GetKnockoutAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaRodadaComPartidasDto>> GetRodadasComPartidasAsync(Guid ligaId, CancellationToken ct);
}
