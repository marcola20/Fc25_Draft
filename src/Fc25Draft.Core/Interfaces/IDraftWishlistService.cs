using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IDraftWishlistService
{
    /// <summary>Todas as versões, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<DraftWishlistEdicaoDto>> GetEdicoesAsync(CancellationToken ct = default);

    /// <summary>Retorna a lista do time identificado pelo token na versão informada (ou na atual, se nula). Vazia se ainda não enviou.</summary>
    Task<DraftWishlistDto> GetByTokenAsync(string token, int? versao = null, CancellationToken ct = default);

    /// <summary>Substitui a lista do time na versão atual pelos jogadores informados, na ordem recebida.</summary>
    Task<DraftWishlistDto> SaveAsync(string token, IReadOnlyList<int> playerIds, CancellationToken ct = default);

    /// <summary>Todas as listas enviadas na versão informada (ou na atual). Uso administrativo.</summary>
    Task<IReadOnlyList<DraftWishlistDto>> GetAllAsync(int? versao = null, CancellationToken ct = default);

    /// <summary>Jogadores com votos na versão informada (ou na atual), do mais votado para o menos. Uso administrativo.</summary>
    Task<IReadOnlyList<DraftWishlistVoteDto>> GetVotesAsync(int? versao = null, CancellationToken ct = default);

    /// <summary>Encerra a versão aberta e cria a próxima, vazia. As listas anteriores ficam preservadas.</summary>
    Task<DraftWishlistEdicaoDto> AbrirNovaEdicaoAsync(string? nome, CancellationToken ct = default);

    /// <summary>Encerra ou reabre os envios de uma versão. Reabrir uma versão encerra as demais.</summary>
    Task<DraftWishlistEdicaoDto> AlterarStatusEdicaoAsync(int numero, bool aberta, CancellationToken ct = default);
}
