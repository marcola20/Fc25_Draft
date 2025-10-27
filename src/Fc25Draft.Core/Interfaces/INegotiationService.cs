using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface INegotiationService
{
    Task<Negotiation> CreateAsync(NegotiationCreateDto dto, CancellationToken ct);

    Task<Negotiation> RespondAsync(Guid negotiationId, NegotiationResponseDto dto, CancellationToken ct);

    Task CancelAsync(Guid negotiationId, Guid teamId, CancellationToken ct);

    Task<IReadOnlyList<Negotiation>> GetActiveAsync(CancellationToken ct);

    Task ForceActionAsync(Guid negotiationId, string action, CancellationToken ct);
}
