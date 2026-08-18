using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ILigaPublicService
{
    Task<LigaDto?> GetAtualAsync(CancellationToken ct);
    Task<LigaDto?> GetByIdAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaDto>> ListAtivasAsync(CancellationToken ct);
    Task<IReadOnlyList<LigaClassificacaoItemDto>> GetClassificacaoAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaArtilheiroDto>> GetArtilheirosAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaArtilheiroDto>> GetAssistenciasAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaCartaoEstatDto>> GetCartoesEstatAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaKnockoutJogoDto>> GetKnockoutAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaRodadaComPartidasDto>> GetRodadasComPartidasAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaGrupoTimeDto>> GetGruposAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaEventoDto>> GetEventosPartidaAsync(Guid partidaId, CancellationToken ct);
    Task<IReadOnlyList<HistoricoArtilheiroDto>> GetHistoricoArtilheirosAsync(CancellationToken ct);
    Task<HistoricoArtilheiroDto?> GetHistoricoArtilheiroDetalheAsync(int jogadorId, CancellationToken ct);

    /// <summary>Histórico de gols/assistências dos jogadores enquanto defenderam este time (inclui quem já saiu).</summary>
    Task<TimeHistoricoDto?> GetHistoricoTimeAsync(Guid timeId, CancellationToken ct);

    /// <summary>Campanha do time nas competições ativas + números do elenco atual na temporada.</summary>
    Task<TimeTemporadaDto?> GetTemporadaTimeAsync(Guid timeId, CancellationToken ct);
}
