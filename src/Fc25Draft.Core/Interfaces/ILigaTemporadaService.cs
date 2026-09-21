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

    /// <summary>Cria as ligas da temporada seguinte com os times já promovidos e rebaixados.</summary>
    Task<IReadOnlyList<LigaDto>> GerarProximaTemporadaAsync(GerarProximaTemporadaRequest request, CancellationToken ct);
}
