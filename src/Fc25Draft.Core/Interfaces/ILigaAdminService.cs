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
    Task<LigaDto> ReverterParaPrimeiraFaseAsync(Guid ligaId, CancellationToken ct);
    Task<LigaDto> ConcluirMiniLigaAsync(Guid ligaId, CancellationToken ct);
    Task<LigaDto> ConcluirDecisaoCampeaoAsync(Guid ligaId, CancellationToken ct);
    Task DeleteLigaAsync(Guid ligaId, CancellationToken ct);

    // Rodadas
    Task<LigaRodadaDto> CreateRodadaAsync(Guid ligaId, CancellationToken ct);
    Task<IReadOnlyList<LigaRodadaDto>> ListRodadasAsync(Guid ligaId, CancellationToken ct);
    /// <summary>Marca as rodadas com as datas do calendário da temporada. Retorna quantas foram marcadas.</summary>
    Task<int> AplicarCalendarioAsync(Guid ligaId, CancellationToken ct);

    Task<IReadOnlyList<LigaRodadaDto>> GerarRodadasAutoAsync(Guid ligaId, CancellationToken ct);
    Task DeleteRodadaAsync(Guid rodadaId, CancellationToken ct);

    // Partidas
    Task<LigaPartidaDto?> GetPartidaByIdAsync(Guid partidaId, CancellationToken ct);
    Task<LigaPartidaDto> CreatePartidaAsync(Guid rodadaId, LigaPartidaCreateRequest request, CancellationToken ct);
    Task<IReadOnlyList<LigaPartidaDto>> ListPartidasAsync(Guid rodadaId, CancellationToken ct);
    Task<LigaPartidaDto> IniciarPartidaAsync(Guid partidaId, CancellationToken ct);
    Task<LigaPartidaDto> EncerrarPartidaAsync(Guid partidaId, CancellationToken ct);
    Task<LigaPartidaDto> EncerrarPartidaComPenaltisAsync(Guid partidaId, Guid vencedorId, CancellationToken ct);
    Task<LigaPartidaDto> AplicarWOAsync(Guid partidaId, Guid timeWOId, CancellationToken ct);
    Task DeletePartidaAsync(Guid partidaId, CancellationToken ct);

    // Eventos
    Task<LigaEventoDto> AddGolAsync(Guid partidaId, LigaGolRequest request, CancellationToken ct);
    Task<LigaEventoDto> AddCartaoAsync(Guid partidaId, LigaCartaoRequest request, CancellationToken ct);
    Task<LigaEventoDto> AddSubstituicaoAsync(Guid partidaId, LigaSubstituicaoRequest request, CancellationToken ct);
    Task DeleteEventoAsync(Guid eventoId, CancellationToken ct);
    Task<IReadOnlyList<LigaEventoDto>> ListEventosAsync(Guid partidaId, CancellationToken ct);

    /// <summary>Recalcula pontos, cartões e posições da competição a que a rodada pertence.</summary>
    Task RecalcularClassificacaoAsync(Guid rodadaId, CancellationToken ct);

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

    /// <summary>Empates de Pts/V/SG nos grupos e o jogo decisivo de cada um, quando já criado.</summary>
    Task<IReadOnlyList<LigaEmpateCopaDto>> ListEmpatesCopaAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Cria o jogo decisivo entre dois times empatados de um grupo (não vale pontos).</summary>
    Task<LigaPartidaDto> GerarJogoDecisivoCopaAsync(Guid ligaId, Guid timeAId, Guid timeBId, CancellationToken ct);
    /// <summary>
    /// Liga com acesso/rebaixamento: empates totais nas posições que mudam de zona, depois
    /// que todos os jogos regulares terminaram, e o jogo decisivo de cada um.
    /// </summary>
    Task<IReadOnlyList<LigaEmpateZonaDto>> ListEmpatesZonaAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Cria o jogo decisivo entre dois times com empate total numa posição de zona (não vale pontos).</summary>
    Task<LigaPartidaDto> GerarJogoDecisivoZonaAsync(Guid ligaId, Guid timeAId, Guid timeBId, CancellationToken ct);

    /// <summary>Potes do sorteio da Copa, com o grupo de cada time quando o sorteio já ocorreu.</summary>
    Task<LigaCopaSorteioDto?> GetSorteioCopaAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Define em que pote cada time da Copa entra (pote 0 ou ausente = fora da Copa).</summary>
    Task<LigaCopaSorteioDto> ConfigurarPotesCopaAsync(Guid ligaId, LigaCopaPotesRequest request, CancellationToken ct);

    /// <summary>Sorteia os grupos: cada pote distribui seus times igualmente entre os grupos.</summary>
    Task<IReadOnlyList<LigaGrupoTimeDto>> SortearCopaAsync(Guid ligaId, CancellationToken ct);

    Task ConfigurarGruposCopaAsync(Guid ligaId, LigaConfigurarGruposRequest request, CancellationToken ct);

    // Times inscritos (Liga de pontos corridos)
    Task<IReadOnlyList<Guid>> ListTimesLigaAsync(Guid ligaId, CancellationToken ct);
    Task ConfigurarTimesLigaAsync(Guid ligaId, IReadOnlyList<Guid> teamIds, CancellationToken ct);

    /// <summary>Define (ou limpa, com nulos) o confronto que deve fechar a temporada na última rodada.</summary>
    Task<LigaDto> DefinirConfrontoFinalAsync(Guid ligaId, Guid? timeAId, Guid? timeBId, CancellationToken ct);

    // Tiebreaker (Liga)
    Task<LigaDto> IniciarDecisaoCampeaoAsync(Guid ligaId, CancellationToken ct);
    Task<LigaDto> IniciarMiniLigaAsync(Guid ligaId, CancellationToken ct);
}
