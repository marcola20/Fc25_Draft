using System.Globalization;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class TransferOfferService : ITransferOfferService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _timeProvider;
    private static readonly CultureInfo BrCulture = CultureInfo.GetCultureInfo("pt-BR");

    public TransferOfferService(DraftDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TransferOfferListItemDto> CreateOfferAsync(CreateTransferOfferDto dto, CancellationToken ct)
    {
        if (dto.FromTeamId == Guid.Empty) throw new ArgumentException("Time de origem inválido.");
        if (dto.ToTeamId == Guid.Empty) throw new ArgumentException("Time de destino inválido.");
        if (dto.FromTeamId == dto.ToTeamId) throw new ArgumentException("Os times devem ser diferentes.");
        var distinctTargets = (dto.TargetPlayerIds ?? Array.Empty<Guid>()).Distinct().ToArray();
        var distinctOffered = (dto.OfferedPlayerIds ?? Array.Empty<Guid>()).Distinct().ToArray();

        if (distinctTargets.Length == 0 && distinctOffered.Length == 0)
            throw new ArgumentException("Selecione ao menos um jogador na negociação.");

        if (dto.Type == OfferType.Swap && distinctOffered.Length == 0)
            throw new ArgumentException("Selecione ao menos um jogador para enviar na troca.");

        if (dto.Money > 0 && dto.MoneyPayerTeamId is null)
            throw new ArgumentException("Informe qual time paga o valor em dinheiro.");

        if (dto.MoneyPayerTeamId.HasValue &&
            dto.MoneyPayerTeamId.Value != dto.FromTeamId && dto.MoneyPayerTeamId.Value != dto.ToTeamId)
            throw new ArgumentException("O time pagador deve ser um dos times envolvidos.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var fromTeam = await _db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TeamId == dto.FromTeamId, ct)
            ?? throw new KeyNotFoundException("Time de origem não encontrado.");

        var toTeam = await _db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TeamId == dto.ToTeamId, ct)
            ?? throw new KeyNotFoundException("Time de destino não encontrado.");

        if (dto.Type == OfferType.Swap)
        {
            if (fromTeam.TransferCount >= 5)
                throw new InvalidOperationException($"O time {fromTeam.TeamName} já atingiu o limite de transferências da janela (5/5).");
            if (toTeam.TransferCount >= 5)
                throw new InvalidOperationException($"O time {toTeam.TeamName} já atingiu o limite de transferências da janela (5/5).");
        }
        else
        {
            if (fromTeam.TransferCount >= 5)
                throw new InvalidOperationException($"O time {fromTeam.TeamName} já atingiu o limite de transferências da janela (5/5).");
        }

        var targetPlayers = await _db.Players
            .Include(p => p.Position)
            .Include(p => p.TeamRosters)
            .Where(p => distinctTargets.Contains(p.PlayerGuid))
            .ToListAsync(ct);

        if (targetPlayers.Count != distinctTargets.Length)
            throw new InvalidOperationException("Um ou mais jogadores alvo não foram encontrados.");

        if (targetPlayers.Any(p => !PlayerBelongsToTeam(p, dto.ToTeamId)))
            throw new InvalidOperationException("Todos os jogadores alvo devem pertencer ao time de destino.");

        var offeredPlayers = new List<Player>();
        if (distinctOffered.Length > 0)
        {
            offeredPlayers = await _db.Players
                .Include(p => p.Position)
                .Include(p => p.TeamRosters)
                .Where(p => distinctOffered.Contains(p.PlayerGuid))
                .ToListAsync(ct);

            if (offeredPlayers.Count != distinctOffered.Length)
                throw new InvalidOperationException("Um ou mais jogadores oferecidos não foram encontrados.");

            if (offeredPlayers.Any(p => !PlayerBelongsToTeam(p, dto.FromTeamId)))
                throw new InvalidOperationException("Todos os jogadores oferecidos devem pertencer ao seu time.");
        }

        if (dto.ParentOfferId.HasValue)
        {
            var parent = await _db.TransferOffers
                .FirstOrDefaultAsync(o => o.OfferId == dto.ParentOfferId.Value, ct)
                ?? throw new KeyNotFoundException("Proposta original não encontrada.");

            if (parent.Status != OfferStatus.Pending)
                throw new InvalidOperationException("Só é possível contraofertar propostas pendentes.");

            parent.Status = OfferStatus.Countered;
            parent.UpdatedAtUtc = now;
        }

        var offer = new TransferOffer
        {
            OfferId = Guid.NewGuid(),
            FromTeamId = dto.FromTeamId,
            ToTeamId = dto.ToTeamId,
            Type = dto.Type,
            Status = OfferStatus.Pending,
            Money = decimal.Round(dto.Money, 2, MidpointRounding.AwayFromZero),
            MoneyPayerTeamId = dto.Money > 0 ? dto.MoneyPayerTeamId : null,
            SellOnPercentage = decimal.Round(dto.SellOnPercentage, 2, MidpointRounding.AwayFromZero),
            Clauses = dto.Clauses?.Trim(),
            Notes = dto.Notes?.Trim(),
            ParentOfferId = dto.ParentOfferId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var offerPlayers = new List<TransferOfferPlayer>();
        foreach (var tp in targetPlayers)
        {
            offerPlayers.Add(new TransferOfferPlayer
            {
                OfferId = offer.OfferId,
                PlayerId = tp.PlayerId,
                IsTarget = true
            });
        }
        foreach (var op in offeredPlayers)
        {
            offerPlayers.Add(new TransferOfferPlayer
            {
                OfferId = offer.OfferId,
                PlayerId = op.PlayerId,
                IsTarget = false
            });
        }

        offer.Players = offerPlayers;

        await _db.TransferOffers.AddAsync(offer, ct);
        await _db.SaveChangesAsync(ct);

        return MapToDto(offer, fromTeam, toTeam, targetPlayers, offeredPlayers);
    }

    public async Task<TransferOfferListItemDto> RespondToOfferAsync(Guid offerId, Guid teamId, OfferStatus response, CancellationToken ct)
    {
        if (response is not (OfferStatus.Accepted or OfferStatus.Rejected))
            throw new ArgumentException("Resposta inválida. Use Accepted ou Rejected.");

        var offer = await _db.TransferOffers
            .Include(o => o.Players).ThenInclude(p => p.Player).ThenInclude(p => p.Position)
            .Include(o => o.FromTeam)
            .Include(o => o.ToTeam)
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct)
            ?? throw new KeyNotFoundException("Proposta não encontrada.");

        if (offer.ToTeamId != teamId)
            throw new InvalidOperationException("Apenas o time destinatário pode responder a esta proposta.");

        if (offer.Status != OfferStatus.Pending)
            throw new InvalidOperationException("Esta proposta já foi respondida.");

        if (response == OfferStatus.Accepted)
        {
            if (offer.Type == OfferType.Swap)
            {
                if (offer.FromTeam.TransferCount >= 5)
                    throw new InvalidOperationException($"O time {offer.FromTeam.TeamName} já atingiu o limite de transferências da janela (5/5).");
                if (offer.ToTeam.TransferCount >= 5)
                    throw new InvalidOperationException($"O time {offer.ToTeam.TeamName} já atingiu o limite de transferências da janela (5/5).");
            }
            else
            {
                if (offer.FromTeam.TransferCount >= 5)
                    throw new InvalidOperationException($"O time {offer.FromTeam.TeamName} já atingiu o limite de transferências da janela (5/5).");
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        offer.Status = response;
        offer.UpdatedAtUtc = now;

        if (response == OfferStatus.Accepted)
        {
            await ExecuteTransferAsync(offer, ct);
            await CancelConflictingOffersAsync(offer, ct);
        }

        await _db.SaveChangesAsync(ct);

        var targetPlayers = offer.Players.Where(p => p.IsTarget).Select(p => p.Player).ToList();
        var offeredPlayers = offer.Players.Where(p => !p.IsTarget).Select(p => p.Player).ToList();

        return MapToDto(offer, offer.FromTeam, offer.ToTeam, targetPlayers, offeredPlayers);
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetReceivedOffersAsync(Guid teamId, CancellationToken ct)
    {
        var offers = await QueryOffers()
            .Where(o => o.ToTeamId == teamId && o.Status == OfferStatus.Pending)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        return offers.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetSentOffersAsync(Guid teamId, CancellationToken ct)
    {
        var offers = await QueryOffers()
            .Where(o => o.FromTeamId == teamId && o.Status == OfferStatus.Pending)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        return offers.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetFinishedOffersAsync(Guid teamId, CancellationToken ct)
    {
        var offers = await QueryOffers()
            .Where(o => (o.FromTeamId == teamId || o.ToTeamId == teamId)
                && (o.Status == OfferStatus.Accepted || o.Status == OfferStatus.Rejected || o.Status == OfferStatus.Countered || o.Status == OfferStatus.Cancelled))
            .OrderByDescending(o => o.UpdatedAtUtc)
            .ToListAsync(ct);

        return offers.Select(MapToDto).ToList();
    }

    public async Task<TransferOfferListItemDto?> GetByIdAsync(Guid offerId, CancellationToken ct)
    {
        var offer = await QueryOffers()
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct);

        return offer is null ? null : MapToDto(offer);
    }

    public async Task<TransferOfferListItemDto> CancelOfferAsync(Guid offerId, Guid teamId, CancellationToken ct)
    {
        var offer = await _db.TransferOffers
            .Include(o => o.Players).ThenInclude(p => p.Player).ThenInclude(p => p.Position)
            .Include(o => o.FromTeam)
            .Include(o => o.ToTeam)
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct)
            ?? throw new KeyNotFoundException("Proposta não encontrada.");

        if (offer.FromTeamId != teamId)
            throw new InvalidOperationException("Apenas o time que enviou a proposta pode cancelá-la.");

        if (offer.Status != OfferStatus.Pending)
            throw new InvalidOperationException("Só é possível cancelar propostas pendentes.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        offer.Status = OfferStatus.Cancelled;
        offer.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);

        var targetPlayers = offer.Players.Where(p => p.IsTarget).Select(p => p.Player).ToList();
        var offeredPlayers = offer.Players.Where(p => !p.IsTarget).Select(p => p.Player).ToList();

        return MapToDto(offer, offer.FromTeam, offer.ToTeam, targetPlayers, offeredPlayers);
    }

    private async Task CancelConflictingOffersAsync(TransferOffer acceptedOffer, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Get all player IDs involved in the accepted offer
        var involvedPlayerIds = acceptedOffer.Players.Select(p => p.PlayerId).ToList();
        if (involvedPlayerIds.Count == 0) return;

        // Find pending offers that involve any of these players
        var conflictingOffers = await _db.TransferOffers
            .Include(o => o.Players)
            .Where(o => o.OfferId != acceptedOffer.OfferId
                && o.Status == OfferStatus.Pending
                && o.Players.Any(p => involvedPlayerIds.Contains(p.PlayerId)))
            .ToListAsync(ct);

        foreach (var offer in conflictingOffers)
        {
            offer.Status = OfferStatus.Cancelled;
            offer.UpdatedAtUtc = now;
        }
    }

    private IQueryable<TransferOffer> QueryOffers()
    {
        return _db.TransferOffers
            .Include(o => o.FromTeam)
            .Include(o => o.ToTeam)
            .Include(o => o.Players).ThenInclude(p => p.Player).ThenInclude(p => p.Position)
            .AsNoTracking();
    }

    private async Task ExecuteTransferAsync(TransferOffer offer, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var targetPlayers = offer.Players.Where(p => p.IsTarget).Select(p => p.Player).ToList();
        var offeredPlayers = offer.Players.Where(p => !p.IsTarget).Select(p => p.Player).ToList();
        var allPlayers = targetPlayers.Concat(offeredPlayers).ToList();

        var fromTeam = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == offer.FromTeamId, ct)
            ?? throw new InvalidOperationException("Time de origem não encontrado.");
        var toTeam = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == offer.ToTeamId, ct)
            ?? throw new InvalidOperationException("Time de destino não encontrado.");

        // Validate roster counts to ensure teams stay with at least 15 players
        var fromTeamRosterCount = await _db.TeamRosters.CountAsync(r => r.TeamId == fromTeam.TeamId, ct);
        var toTeamRosterCount = await _db.TeamRosters.CountAsync(r => r.TeamId == toTeam.TeamId, ct);

        // If toTeam is losing players (targetPlayers), it must have at least 16 to stay with 15
        if (targetPlayers.Count > 0 && toTeamRosterCount < 16)
            throw new InvalidOperationException($"O time {toTeam.TeamName} ficaria com menos de 15 jogadores.");

        // If fromTeam is losing players (offeredPlayers in a swap), it must have at least 16 to stay with 15
        if (offeredPlayers.Count > 0 && offer.Type == OfferType.Swap && fromTeamRosterCount < 16)
            throw new InvalidOperationException($"O time {fromTeam.TeamName} ficaria com menos de 15 jogadores.");

        if (offer.Money > 0 && offer.MoneyPayerTeamId.HasValue)
        {
            var payerTeam = offer.MoneyPayerTeamId.Value == fromTeam.TeamId ? fromTeam : toTeam;
            var receiverTeam = offer.MoneyPayerTeamId.Value == fromTeam.TeamId ? toTeam : fromTeam;

            var available = decimal.Round(payerTeam.Budget - payerTeam.BudgetBlocked, 2, MidpointRounding.AwayFromZero);
            if (available < offer.Money)
                throw new InvalidOperationException($"Saldo insuficiente no time {payerTeam.TeamName}.");

            payerTeam.Budget = decimal.Round(payerTeam.Budget - offer.Money, 2, MidpointRounding.AwayFromZero);
            receiverTeam.Budget = decimal.Round(receiverTeam.Budget + offer.Money, 2, MidpointRounding.AwayFromZero);
        }

        var amountCarrierPlayerId = allPlayers.Count > 0
            ? allPlayers.OrderByDescending(p => p.Overall).ThenBy(p => p.PlayerId).First().PlayerId
            : (int?)null;

        var noteParts = new List<string>();
        if (offer.Type == OfferType.Swap)
        {
            noteParts.Add($"Troca entre {fromTeam.TeamName} e {toTeam.TeamName}");
            if (targetPlayers.Count > 0)
                noteParts.Add($"{toTeam.TeamName} envia: {FormatPlayerList(targetPlayers)}");
            if (offeredPlayers.Count > 0)
                noteParts.Add($"{fromTeam.TeamName} envia: {FormatPlayerList(offeredPlayers)}");
        }
        else
        {
            var typeLabel = offer.Type == OfferType.Loan ? "Empréstimo" : "Venda";
            if (targetPlayers.Count > 0)
            {
                noteParts.Add($"{typeLabel} de {FormatPlayerList(targetPlayers)}");
                noteParts.Add($"De {toTeam.TeamName} para {fromTeam.TeamName}");
            }
            if (offeredPlayers.Count > 0)
            {
                noteParts.Add($"{typeLabel} de {FormatPlayerList(offeredPlayers)}");
                noteParts.Add($"De {fromTeam.TeamName} para {toTeam.TeamName}");
            }
        }

        if (offer.Money > 0 && offer.MoneyPayerTeamId.HasValue)
        {
            var payerName = offer.MoneyPayerTeamId.Value == fromTeam.TeamId ? fromTeam.TeamName : toTeam.TeamName;
            noteParts.Add($"{payerName} paga {offer.Money.ToString("C", BrCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(offer.Clauses))
            noteParts.Add($"Cláusulas: {offer.Clauses}");

        if (offer.SellOnPercentage > 0)
            noteParts.Add($"Revenda futura: {offer.SellOnPercentage:N2}%");

        var historyNotes = string.Join(". ", noteParts);
        if (historyNotes.Length > 400)
            historyNotes = historyNotes[..397] + "...";

        foreach (var player in targetPlayers)
        {
            var tracked = await _db.Players.FirstAsync(p => p.PlayerId == player.PlayerId, ct);
            tracked.CurrentTeamId = offer.FromTeamId;

            var oldRosters = await _db.TeamRosters
                .Where(r => r.PlayerId == player.PlayerId && r.TeamId != offer.FromTeamId)
                .ToListAsync(ct);
            _db.TeamRosters.RemoveRange(oldRosters);

            var exists = await _db.TeamRosters.AnyAsync(r => r.PlayerId == player.PlayerId && r.TeamId == offer.FromTeamId, ct);
            if (!exists)
                await _db.TeamRosters.AddAsync(new TeamRoster { PlayerId = player.PlayerId, TeamId = offer.FromTeamId }, ct);

            await _db.TransferHistories.AddAsync(new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = offer.Type == OfferType.Swap ? TransferType.TeamTrade : TransferType.TeamSale,
                PlayerId = player.PlayerId,
                FromTeamId = offer.ToTeamId,
                ToTeamId = offer.FromTeamId,
                Amount = amountCarrierPlayerId == player.PlayerId ? offer.Money : 0m,
                Notes = historyNotes,
                PerformedBy = "system",
                PerformedAtUtc = now,
                OldOverall = player.Overall,
                NewOverall = player.Overall
            }, ct);
        }

        foreach (var player in offeredPlayers)
        {
            var tracked = await _db.Players.FirstAsync(p => p.PlayerId == player.PlayerId, ct);
            tracked.CurrentTeamId = offer.ToTeamId;

            var oldRosters = await _db.TeamRosters
                .Where(r => r.PlayerId == player.PlayerId && r.TeamId != offer.ToTeamId)
                .ToListAsync(ct);
            _db.TeamRosters.RemoveRange(oldRosters);

            var exists = await _db.TeamRosters.AnyAsync(r => r.PlayerId == player.PlayerId && r.TeamId == offer.ToTeamId, ct);
            if (!exists)
                await _db.TeamRosters.AddAsync(new TeamRoster { PlayerId = player.PlayerId, TeamId = offer.ToTeamId }, ct);

            await _db.TransferHistories.AddAsync(new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.TeamTrade,
                PlayerId = player.PlayerId,
                FromTeamId = offer.FromTeamId,
                ToTeamId = offer.ToTeamId,
                Amount = amountCarrierPlayerId == player.PlayerId ? offer.Money : 0m,
                Notes = historyNotes,
                PerformedBy = "system",
                PerformedAtUtc = now,
                OldOverall = player.Overall,
                NewOverall = player.Overall
            }, ct);
        }

        fromTeam.TransferCount++;
        if (offer.Type == OfferType.Swap)
            toTeam.TransferCount++;

        var teamsAtLimit = new List<Guid>();
        if (fromTeam.TransferCount >= 5) teamsAtLimit.Add(fromTeam.TeamId);
        if (offer.Type == OfferType.Swap && toTeam.TransferCount >= 5) teamsAtLimit.Add(toTeam.TeamId);

        if (teamsAtLimit.Count > 0)
        {
            var now2 = _timeProvider.GetUtcNow().UtcDateTime;
            var offersToCancel = await _db.TransferOffers
                .Where(o => o.OfferId != offer.OfferId
                    && o.Status == OfferStatus.Pending
                    && (teamsAtLimit.Contains(o.FromTeamId) || teamsAtLimit.Contains(o.ToTeamId)))
                .ToListAsync(ct);

            foreach (var o in offersToCancel)
            {
                o.Status = OfferStatus.Cancelled;
                o.UpdatedAtUtc = now2;
            }
        }
    }

    private static string FormatPlayerList(IReadOnlyCollection<Player> players)
        => players.Count == 0 ? "Sem jogadores" : string.Join(", ", players.Select(p => $"{p.Name} ({p.Overall})"));

    private static TransferOfferListItemDto MapToDto(TransferOffer offer)
    {
        var targetPlayers = offer.Players.Where(p => p.IsTarget).Select(p => p.Player).ToList();
        var offeredPlayers = offer.Players.Where(p => !p.IsTarget).Select(p => p.Player).ToList();
        return MapToDto(offer, offer.FromTeam, offer.ToTeam, targetPlayers, offeredPlayers);
    }

    private static TransferOfferListItemDto MapToDto(
        TransferOffer offer, Team fromTeam, Team toTeam,
        IReadOnlyCollection<Player> targetPlayers, IReadOnlyCollection<Player> offeredPlayers)
    {
        string? moneyPayerName = null;
        if (offer.MoneyPayerTeamId.HasValue)
        {
            moneyPayerName = offer.MoneyPayerTeamId.Value == fromTeam.TeamId
                ? fromTeam.TeamName
                : toTeam.TeamName;
        }

        return new TransferOfferListItemDto(
            offer.OfferId,
            offer.FromTeamId,
            fromTeam.TeamName,
            offer.ToTeamId,
            toTeam.TeamName,
            offer.Type.ToString(),
            offer.Status.ToString(),
            offer.Money,
            offer.MoneyPayerTeamId,
            moneyPayerName,
            offer.SellOnPercentage,
            offer.Clauses,
            offer.Notes,
            offer.ParentOfferId,
            targetPlayers.Select(MapPlayerDto).ToList(),
            offeredPlayers.Select(MapPlayerDto).ToList(),
            offer.CreatedAtUtc,
            offer.UpdatedAtUtc);
    }

    private static TransferOfferPlayerDto MapPlayerDto(Player p)
    {
        return new TransferOfferPlayerDto(
            p.PlayerId,
            p.PlayerGuid,
            p.Name,
            p.Position?.Name ?? "?",
            p.Overall,
            p.Age);
    }

    private static bool PlayerBelongsToTeam(Player player, Guid teamId)
    {
        if (player.CurrentTeamId == teamId)
            return true;

        return player.TeamRosters?.Any(r => r.TeamId == teamId) ?? false;
    }
}
