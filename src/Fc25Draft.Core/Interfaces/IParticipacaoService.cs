using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Participação dos jogadores no clube atual (<see cref="Participacao"/>): mexe no valor de mercado e diz quem está
/// insatisfeito com o banco.
/// </summary>
public interface IParticipacaoService
{
    /// <summary>Todos os jogadores com clube. A conta é guardada por um minuto.</summary>
    Task<IReadOnlyDictionary<int, ParticipacaoJogador>> GetTodosAsync(CancellationToken ct);

    /// <summary>A conta de um jogador; nulo se ele não tem clube.</summary>
    Task<ParticipacaoJogador?> GetAsync(int playerId, CancellationToken ct);

    /// <summary>A conta depois de cada jogo do clube atual, desde a chegada; vazio se ele não tem clube.</summary>
    Task<IReadOnlyList<PontoDeParticipacao>> GetHistoricoAsync(int playerId, CancellationToken ct);

    /// <summary>
    /// Como estava em <paramref name="ateUtc"/>: a conta dos jogadores com clube hoje, só com os jogos até essa data
    /// (sem guardar).
    /// </summary>
    Task<IReadOnlyDictionary<int, ParticipacaoJogador>> GetTodosEmAsync(DateTime ateUtc, CancellationToken ct);

    /// <summary>Notícias do Plantão: quem pediu para sair e quem fez as pazes.</summary>
    Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct);

    /// <summary>Avisa o técnico quando um jogador dele fica insatisfeito (jogos recentes). Retorna quantos avisos.</summary>
    Task<int> AvisarInsatisfeitosAsync(CancellationToken ct);

    /// <summary>Avisa o técnico quando um jogador fica 😐 incomodado (reclama se não jogar o próximo). Retorna quantos.</summary>
    Task<int> AvisarIncomodadosAsync(CancellationToken ct);

    /// <summary>
    /// Grava o papel de quem chegou depois que a regra do papel entrou no ar (pela chegada e pelo elenco de agora, que
    /// é o que ele encontrou). Retorna quantos gravou.
    /// </summary>
    Task<int> GravarPapeisAsync(CancellationToken ct);

    /// <summary>
    /// Papel de cada jogador com clube na temporada nova, pelo que jogou em <paramref name="temporada"/> (a que acabou).
    /// Precisa da regra do papel no ar.
    /// </summary>
    Task<IReadOnlyDictionary<int, PapelNoElenco>> PapeisNaViradaAsync(int temporada, CancellationToken ct);
}
