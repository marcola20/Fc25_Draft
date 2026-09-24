using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Painel do técnico: o que importa para o time logado agora, e a lista de observação.</summary>
public interface IMinhaAreaService
{
    /// <summary>Time dono do token (titular ou auxiliar), ou nulo.</summary>
    Task<(Guid TeamId, string TeamName)?> ResolverTimeAsync(string? token, CancellationToken ct);

    Task<MinhaAreaDto> GetAsync(string? token, CancellationToken ct);

    Task<bool> EstaObservandoAsync(string? token, int playerId, CancellationToken ct);

    /// <summary>Marca ou desmarca o jogador na lista de observação; devolve se ficou observado.</summary>
    Task<bool> AlternarObservacaoAsync(string? token, int playerId, CancellationToken ct);
}
