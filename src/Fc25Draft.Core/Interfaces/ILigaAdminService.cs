using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ILigaAdminService
{
    // Liga
    Task<LigaDto> CreateAsync(LigaCreateRequest request, CancellationToken ct);
    Task<LigaDto?> GetByIdAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaDto>> ListAsync(CancellationToken ct);
    Task<LigaDto> UpdateAsync(Guid ligaId, LigaUpdateRequest request, CancellationToken ct);
    Task<LigaDto> IniciarPrimeiraFaseAsync(Guid ligaId, CancellationToken ct);
    Task<LigaDto> EncerrarPrimeiraFaseAsync(Guid ligaId, CancellationToken ct);
    Task DeleteLigaAsync(Guid ligaId, CancellationToken ct);

    // Rodadas
    Task<LigaRodadaDto> CreateRodadaAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaRodadaDto>> ListRodadasAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaRodadaDto>> GerarRodadasAutoAsync(Guid ligaId, CancellationToken ct);
    Task DeleteRodadaAsync(Guid rodadaId, CancellationToken ct);

    // Partidas
    Task<LigaPartidaDto?> GetPartidaByIdAsync(Guid partidaId, CancellationToken ct);
    Task<LigaPartidaDto> CreatePartidaAsync(Guid rodadaId, LigaPartidaCreateRequest request, CancellationToken ct);
    Task<IReadOnlyList<LigaPartidaDto>> ListPartidasAsync(Guid rodadaId, CancellationToken ct);
    Task<LigaPartidaDto> IniciarPartidaAsync(Guid partidaId, CancellationToken ct);
    Task<LigaPartidaDto> EncerrarPartidaAsync(Guid partidaId, CancellationToken ct);
    Task<LigaPartidaDto> AplicarWOAsync(Guid partidaId, Guid timeWOId, CancellationToken ct);
    Task DeletePartidaAsync(Guid partidaId, CancellationToken ct);

    // Eventos
    Task<LigaEventoDto> AddGolAsync(Guid partidaId, LigaGolRequest request, CancellationToken ct);
    Task<LigaEventoDto> AddCartaoAsync(Guid partidaId, LigaCartaoRequest request, CancellationToken ct);
    Task DeleteEventoAsync(Guid eventoId, CancellationToken ct);
    Task<IReadOnlyList<LigaEventoDto>> ListEventosAsync(Guid partidaId, CancellationToken ct);

    // Punições
    Task<LigaPunicaoDto> AplicarPunicaoAsync(Guid ligaId, LigaPunicaoRequest request, CancellationToken ct);
    Task<IReadOnlyList<LigaPunicaoDto>> ListPunicoesAsync(Guid ligaId, CancellationToken ct);
    Task RemoverPunicaoAsync(Guid punicaoId, CancellationToken ct);

    // Knockout
    Task<IReadOnlyList<LigaKnockoutJogoDto>> GerarFaseKnockoutAsync(Guid ligaId, CancellationToken ct);
    Task<LigaPartidaDto> CriarPartidaKnockoutAsync(Guid knockoutJogoId, CancellationToken ct);
    Task<LigaKnockoutJogoDto> EncerrarKnockoutJogoAsync(Guid knockoutJogoId, LigaEncerrarKnockoutRequest request, CancellationToken ct);

    // Copa grupos
    Task<IReadOnlyList<LigaGrupoTimeDto>> ListGruposAsync(Guid ligaId, CancellationToken ct);
    Task ConfigurarGruposCopaAsync(Guid ligaId, LigaConfigurarGruposRequest request, CancellationToken ct);

    // Tiebreaker (Liga)
    Task<LigaDto> IniciarDecisaoCampeaoAsync(Guid ligaId, CancellationToken ct);
    Task<LigaDto> IniciarMiniLigaAsync(Guid ligaId, CancellationToken ct);
}
