using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IDraftWishlistService
{
    /// <summary>Retorna a lista do time identificado pelo token (vazia se ainda não enviou).</summary>
    Task<DraftWishlistDto> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Substitui a lista do time pelos jogadores informados, na ordem recebida.</summary>
    Task<DraftWishlistDto> SaveAsync(string token, IReadOnlyList<int> playerIds, CancellationToken ct = default);

    /// <summary>Todas as listas enviadas (uso administrativo).</summary>
    Task<IReadOnlyList<DraftWishlistDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Jogadores com votos, do mais votado para o menos (uso administrativo).</summary>
    Task<IReadOnlyList<DraftWishlistVoteDto>> GetVotesAsync(CancellationToken ct = default);
}
