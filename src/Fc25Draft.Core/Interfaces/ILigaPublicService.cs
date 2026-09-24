using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ILigaPublicService
{
    Task<LigaDto?> GetAtualAsync(CancellationToken ct);
    Task<LigaDto?> GetByIdAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaDto>> ListAtivasAsync(CancellationToken ct);

    /// <summary>Todas as edições (em andamento e encerradas) com o resumo do formato de cada uma.</summary>
    Task<IReadOnlyList<LigaEdicaoDto>> ListEdicoesAsync(CancellationToken ct);
    Task<IReadOnlyList<LigaClassificacaoItemDto>> GetClassificacaoAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaArtilheiroDto>> GetArtilheirosAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaArtilheiroDto>> GetAssistenciasAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaCartaoEstatDto>> GetCartoesEstatAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaKnockoutJogoDto>> GetKnockoutAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaRodadaComPartidasDto>> GetRodadasComPartidasAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaGrupoTimeDto>> GetGruposAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaEventoDto>> GetEventosPartidaAsync(Guid partidaId, CancellationToken ct);
    Task<PartidaEscalacoesDto?> GetEscalacoesPartidaAsync(Guid partidaId, CancellationToken ct);
    Task<IReadOnlyList<HistoricoArtilheiroDto>> GetHistoricoArtilheirosAsync(CancellationToken ct);
    Task<HistoricoArtilheiroDto?> GetHistoricoArtilheiroDetalheAsync(int jogadorId, CancellationToken ct);

    /// <summary>Histórico de gols/assistências dos jogadores enquanto defenderam este time (inclui quem já saiu).</summary>
    Task<TimeHistoricoDto?> GetHistoricoTimeAsync(Guid timeId, CancellationToken ct);

    /// <summary>Trajetória do time por temporada: divisão, posição, títulos, acessos e rebaixamentos.</summary>
    Task<IReadOnlyList<TimeTrajetoriaDto>> GetTrajetoriaTimeAsync(Guid timeId, CancellationToken ct);

    /// <summary>Campanha do time nas competições ativas + números do elenco atual na temporada.</summary>
    Task<TimeTemporadaDto?> GetTemporadaTimeAsync(Guid timeId, CancellationToken ct);

    /// <summary>Jogos do time em todas as competições: forma, sequências, recordes, próximos jogos e confrontos.</summary>
    Task<TimePerfilDto> GetPerfilTimeAsync(Guid timeId, CancellationToken ct);

    /// <summary>Chegadas e saídas do time, com o que gastou e recebeu.</summary>
    Task<TimeTransferenciasDto> GetTransferenciasTimeAsync(Guid timeId, CancellationToken ct);

    /// <summary>Carreira do jogador: números por competição e time, títulos e trajetória (draft e transferências).</summary>
    Task<JogadorCarreiraDto?> GetCarreiraJogadorAsync(int jogadorId, CancellationToken ct);

    /// <summary>
    /// Ranking de Clubes de todas as temporadas, calculado na hora a partir dos jogos já
    /// encerrados (inclusive os da temporada em andamento), títulos, finais, semis e posições.
    /// </summary>
    Task<IReadOnlyList<RankingClubeDto>> GetRankingClubesAsync(CancellationToken ct);
}
