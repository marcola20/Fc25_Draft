using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Fim de temporada: playoff de acesso entre as divisões e criação da temporada seguinte.</summary>
public interface ILigaTemporadaService
{
    /// <summary>Temporadas com liga cadastrada, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<int>> ListTemporadasAsync(CancellationToken ct);

    /// <summary>Quem sobe, quem cai, o playoff e o que falta para virar a temporada.</summary>
    Task<TemporadaResumoDto?> GetResumoAsync(int temporada, CancellationToken ct);

    /// <summary>Cria o jogo único de cada vaga de playoff (Série A x Série B). Não vale pontos.</summary>
    Task<IReadOnlyList<TemporadaPlayoffDto>> CriarPlayoffAcessoAsync(int temporada, CancellationToken ct);

    /// <summary>Cria a Supercopa da temporada: jogo único entre o campeão da Série A e o campeão da Copa.</summary>
    Task<LigaDto> CriarSupercopaAsync(int temporada, string? nome, DateTime data, CancellationToken ct);

    /// <summary>Dia em que a temporada abre (o da Supercopa); nulo se ainda não foi definido.</summary>
    Task<DateTime?> GetAberturaAsync(int temporada, CancellationToken ct);

    /// <summary>
    /// Grava a abertura da temporada. Com <paramref name="reaplicarNasRodadas"/>, as rodadas ainda não
    /// jogadas das competições da temporada (e a Supercopa que a abre) voltam para as datas do
    /// calendário, inclusive as que foram mudadas à mão. Retorna quantas rodadas foram remarcadas.
    /// </summary>
    Task<int> DefinirAberturaAsync(int temporada, DateTime abertura, bool reaplicarNasRodadas, CancellationToken ct);

    /// <summary>Empréstimos, propostas, lista de transferências e contadores que a virada vai mexer.</summary>
    Task<TemporadaViradaMercadoDto> GetViradaMercadoAsync(CancellationToken ct);

    /// <summary>
    /// Cria as ligas da temporada seguinte com os times já promovidos e rebaixados, devolve os
    /// emprestados e faz a limpeza do mercado pedida — tudo numa transação: ou faz tudo, ou nada.
    /// </summary>
    Task<IReadOnlyList<LigaDto>> GerarProximaTemporadaAsync(GerarProximaTemporadaRequest request, CancellationToken ct);
}
