using System.Collections.Generic;
using System.Linq;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class TransferOfferService : ITransferOfferService
{
    private const int MaxMessageLength = 400;
    private readonly DraftDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public TransferOfferService(DraftDbContext dbContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TransferOffer> CreateOfferAsync(
        Guid fromTeamId,
        Guid toTeamId,
        int playerId,
        decimal? offeredFee,
        IEnumerable<int> swapPlayerIds,
        string? message,
        DateTime? expiresAtUtc,
        CancellationToken ct)
    {
        if (fromTeamId == Guid.Empty)
        {
            throw new ArgumentException("fromTeamId é obrigatório.", nameof(fromTeamId));
        }

        if (toTeamId == Guid.Empty)
        {
            throw new ArgumentException("toTeamId é obrigatório.", nameof(toTeamId));
        }

        if (fromTeamId == toTeamId)
        {
            throw new ArgumentException("Os times da oferta devem ser diferentes.");
        }

        if (playerId <= 0)
        {
            throw new ArgumentException("playerId é obrigatório.", nameof(playerId));
        }

        var player = await _dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct)
            .ConfigureAwait(false);

        if (player is null)
        {
            throw new InvalidOperationException($"Jogador {playerId} não encontrado.");
        }

        if (player.CurrentTeamId != toTeamId)
        {
            throw new InvalidOperationException("O jogador informado não pertence ao time de destino.");
        }

        var fromTeamExists = await _dbContext.Teams
            .AsNoTracking()
            .AnyAsync(t => t.TeamId == fromTeamId, ct)
            .ConfigureAwait(false);

        if (!fromTeamExists)
        {
            throw new InvalidOperationException($"Time ofertante {fromTeamId} não encontrado.");
        }

        var toTeamExists = await _dbContext.Teams
            .AsNoTracking()
            .AnyAsync(t => t.TeamId == toTeamId, ct)
            .ConfigureAwait(false);

        if (!toTeamExists)
        {
            throw new InvalidOperationException($"Time destinatário {toTeamId} não encontrado.");
        }

        var normalizedSwapIds = swapPlayerIds?.Distinct().ToList() ?? new List<int>();

        var swapPlayers = new List<Player>();
        if (normalizedSwapIds.Count > 0)
        {
            swapPlayers = await _dbContext.Players
                .AsNoTracking()
                .Where(p => normalizedSwapIds.Contains(p.PlayerId))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (swapPlayers.Count != normalizedSwapIds.Count)
            {
                var missing = normalizedSwapIds.Except(swapPlayers.Select(p => p.PlayerId)).ToArray();
                throw new InvalidOperationException($"Jogadores de troca inválidos: {string.Join(", ", missing)}.");
            }

            var invalidOwners = swapPlayers
                .Where(p => p.CurrentTeamId != fromTeamId)
                .Select(p => p.PlayerId)
                .ToArray();

            if (invalidOwners.Length > 0)
            {
                throw new InvalidOperationException($"Jogadores {string.Join(", ", invalidOwners)} não pertencem ao time ofertante.");
            }
        }

        if (offeredFee.HasValue && offeredFee.Value < 0)
        {
            throw new ArgumentException("O valor oferecido não pode ser negativo.", nameof(offeredFee));
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var normalizedExpiresAtUtc = expiresAtUtc.HasValue
            ? NormalizeToUtc(expiresAtUtc.Value)
            : (DateTime?)null;

        if (normalizedExpiresAtUtc.HasValue && normalizedExpiresAtUtc.Value <= utcNow)
        {
            throw new ArgumentException("A data de expiração deve ser futura.", nameof(expiresAtUtc));
        }

        var offer = new TransferOffer
        {
            OfferId = Guid.NewGuid(),
            FromTeamId = fromTeamId,
            ToTeamId = toTeamId,
            PlayerId = playerId,
            OfferedFee = offeredFee,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            ExpiresAtUtc = normalizedExpiresAtUtc,
            Status = TransferOfferStatus.Pending,
            Message = Normalize(message),
        };

        foreach (var swap in swapPlayers)
        {
            offer.SwapPlayers.Add(new TransferOfferSwapPlayer
            {
                SwapPlayerId = Guid.NewGuid(),
                OfferId = offer.OfferId,
                PlayerId = swap.PlayerId,
                TeamId = fromTeamId,
            });
        }

        await _dbContext.TransferOffers.AddAsync(offer, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        await _dbContext.Entry(offer).Collection(o => o.SwapPlayers).LoadAsync(ct).ConfigureAwait(false);

        return offer;
    }

    public async Task<TransferOffer> AcceptOfferAsync(Guid offerId, uint expectedRowVersion, CancellationToken ct)
    {
        var offer = await LoadOfferForUpdateAsync(offerId, ct).ConfigureAwait(false);

        EnsurePending(offer);
        SetOriginalRowVersion(offer, expectedRowVersion);

        offer.Status = TransferOfferStatus.Accepted;
        offer.RespondedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        offer.UpdatedAtUtc = offer.RespondedAtUtc.Value;

        await SaveWithConcurrencyCheckAsync(ct).ConfigureAwait(false);

        return offer;
    }

    public async Task<TransferOffer> RejectOfferAsync(Guid offerId, string? responseMessage, uint expectedRowVersion, CancellationToken ct)
    {
        var offer = await LoadOfferForUpdateAsync(offerId, ct).ConfigureAwait(false);

        EnsurePending(offer);
        SetOriginalRowVersion(offer, expectedRowVersion);

        offer.Status = TransferOfferStatus.Rejected;
        offer.ResponseMessage = Normalize(responseMessage);
        offer.RespondedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        offer.UpdatedAtUtc = offer.RespondedAtUtc.Value;

        await SaveWithConcurrencyCheckAsync(ct).ConfigureAwait(false);

        return offer;
    }

    public async Task<TransferOffer> CancelOfferAsync(Guid offerId, Guid requestingTeamId, uint expectedRowVersion, CancellationToken ct)
    {
        if (requestingTeamId == Guid.Empty)
        {
            throw new ArgumentException("requestingTeamId é obrigatório.", nameof(requestingTeamId));
        }

        var offer = await LoadOfferForUpdateAsync(offerId, ct).ConfigureAwait(false);

        EnsurePending(offer);

        if (offer.FromTeamId != requestingTeamId)
        {
            throw new InvalidOperationException("Apenas o time ofertante pode cancelar a proposta.");
        }

        SetOriginalRowVersion(offer, expectedRowVersion);

        offer.Status = TransferOfferStatus.Canceled;
        offer.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await SaveWithConcurrencyCheckAsync(ct).ConfigureAwait(false);

        return offer;
    }

    public async Task<TransferOffer> ExpireOfferAsync(Guid offerId, CancellationToken ct)
    {
        var offer = await LoadOfferForUpdateAsync(offerId, ct).ConfigureAwait(false);

        if (offer.Status != TransferOfferStatus.Pending)
        {
            return offer;
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        if (offer.ExpiresAtUtc.HasValue && offer.ExpiresAtUtc.Value > utcNow)
        {
            return offer;
        }

        offer.Status = TransferOfferStatus.Expired;
        offer.UpdatedAtUtc = utcNow;

        await SaveWithConcurrencyCheckAsync(ct).ConfigureAwait(false);

        return offer;
    }

    private async Task<TransferOffer> LoadOfferForUpdateAsync(Guid offerId, CancellationToken ct)
    {
        if (offerId == Guid.Empty)
        {
            throw new ArgumentException("offerId é obrigatório.", nameof(offerId));
        }

        var offer = await _dbContext.TransferOffers
            .Include(o => o.SwapPlayers)
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct)
            .ConfigureAwait(false);

        if (offer is null)
        {
            throw new InvalidOperationException($"Oferta {offerId} não encontrada.");
        }

        return offer;
    }

    private static void EnsurePending(TransferOffer offer)
    {
        if (offer.Status != TransferOfferStatus.Pending)
        {
            throw new InvalidOperationException("A oferta não está mais pendente.");
        }
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxMessageLength ? trimmed : trimmed[..MaxMessageLength];
    }

    private void SetOriginalRowVersion(TransferOffer offer, uint expectedRowVersion)
    {
        if (expectedRowVersion == 0)
        {
            throw new ArgumentException("expectedRowVersion é obrigatório.", nameof(expectedRowVersion));
        }

        _dbContext.Entry(offer).Property(o => o.RowVersion).OriginalValue = expectedRowVersion;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private async Task SaveWithConcurrencyCheckAsync(CancellationToken ct)
    {
        try
        {
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException("A oferta foi atualizada por outro processo.", ex);
        }
    }
}
