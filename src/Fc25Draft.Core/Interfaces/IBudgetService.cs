using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IBudgetService
{
    Task<decimal> GetSaldoAsync(Guid teamId, CancellationToken ct);

    Task<decimal> GetBloqueadoEmLancesAsync(Guid teamId, CancellationToken ct);

    Task<decimal> GetSaldoDisponivelAsync(Guid teamId, CancellationToken ct);

    Task<decimal> GetAvailableAsync(Guid teamId, Guid? excludeItemId, CancellationToken ct);

    Task RegistrarAjusteAsync(Guid teamId, decimal valor, string origem, string? descricao, bool credito, CancellationToken ct);

    decimal CalculateMatchRewardAmount(MatchRewardRequestDto request);

    Task<MatchRewardResult> ApplyMatchRewardAsync(MatchRewardRequestDto request, CancellationToken ct);
}
