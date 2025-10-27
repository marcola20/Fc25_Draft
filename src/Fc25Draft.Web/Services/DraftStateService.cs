using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Services;

public class DraftStateService
{
    private readonly DraftDbContext _db;
    private readonly IHubContext<DraftHub> _hubContext;
    private readonly ILogger<DraftStateService> _logger;

    public DraftStateService(
        DraftDbContext db,
        IHubContext<DraftHub> hubContext,
        ILogger<DraftStateService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<DraftStateDto> GetStateAsync(CancellationToken ct = default)
    {
        var draft = await _db.Drafts
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (draft is null)
        {
            return DraftStateDto.Empty;
        }

        var picksQuery = _db.DraftPicks
            .AsNoTracking()
            .Where(p => p.DraftId == draft.DraftId);

        var totalPicks = await picksQuery.CountAsync(ct);
        var completedPicks = await picksQuery.Where(p => p.PlayerId != null).CountAsync(ct);

        var currentPick = await picksQuery
            .Where(p => p.PlayerId == null)
            .OrderBy(p => p.OverallPick)
            .Select(p => new
            {
                p.RoundNumber,
                p.PickInRound,
                p.OverallPick,
                TeamId = p.Team.TeamId,
                TeamName = p.Team.TeamName,
                TeamOwner = p.Team.OwnerName
            })
            .FirstOrDefaultAsync(ct);

        Guid? nextTeamId = null;
        string? nextTeamName = null;
        string? nextTeamOwner = null;

        if (currentPick is not null)
        {
            var nextPick = await picksQuery
                .Where(p => p.PlayerId == null && p.OverallPick > currentPick.OverallPick)
                .OrderBy(p => p.OverallPick)
                .Select(p => new
                {
                    TeamId = p.Team.TeamId,
                    TeamName = p.Team.TeamName,
                    TeamOwner = p.Team.OwnerName
                })
                .FirstOrDefaultAsync(ct);

            if (nextPick is not null)
            {
                nextTeamId = nextPick.TeamId;
                nextTeamName = nextPick.TeamName;
                nextTeamOwner = nextPick.TeamOwner;
            }
        }

        return new DraftStateDto(
            draft.DraftId,
            draft.Name,
            draft.TotalTeams,
            draft.TotalRounds,
            totalPicks,
            completedPicks,
            currentPick?.RoundNumber,
            currentPick?.PickInRound,
            currentPick?.OverallPick,
            currentPick?.TeamId,
            currentPick?.TeamName,
            currentPick?.TeamOwner,
            nextTeamId,
            nextTeamName,
            nextTeamOwner,
            currentPick is null && totalPicks > 0 && completedPicks == totalPicks);
    }

    public async Task<IReadOnlyList<AvailablePlayerDto>> GetAvailablePlayersAsync(short? positionId, CancellationToken ct = default)
    {
        var query = _db.Players
            .AsNoTracking()
            .Include(p => p.Position)
            .Where(p => !_db.DraftPicks.Any(dp => dp.PlayerId == p.PlayerId));

        if (positionId.HasValue)
        {
            query = query.Where(p => p.PositionId == positionId.Value);
        }

        var players = await query
            .OrderByDescending(p => p.Overall)
            .ThenBy(p => p.Name)
            .Select(p => new AvailablePlayerDto(
                p.PlayerId,
                p.Name,
                p.PositionId,
                p.Position.Name,
                p.Overall,
                p.Age))
            .ToListAsync(ct);

        return players;
    }

    public async Task<DraftStateDto> MakePickAsync(int playerId, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("O token do time é obrigatório.", nameof(token));
        }

        var normalizedToken = token.Trim();

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var draft = await _db.Drafts
                .OrderByDescending(d => d.CreatedAtUtc)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Nenhum draft ativo foi encontrado.");

            var currentPick = await _db.DraftPicks
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.DraftId == draft.DraftId && p.PlayerId == null, ct)
                ?? throw new InvalidOperationException("Todas as escolhas já foram realizadas.");

            if (!Guid.TryParse(normalizedToken, out var providedToken) || providedToken != currentPick.Team.TeamToken)
            {
                throw new InvalidOperationException("⚠️ Token inválido para este time.");
            }

            var player = await _db.Players
                .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct)
                ?? throw new InvalidOperationException("Jogador não encontrado.");

            var alreadyChosen = await _db.DraftPicks.AnyAsync(p => p.PlayerId == playerId, ct);
            if (alreadyChosen)
            {
                throw new InvalidOperationException("❌ Este jogador já foi selecionado.");
            }

            var alreadyInRoster = await _db.TeamRosters.AnyAsync(r => r.PlayerId == playerId, ct);
            if (alreadyInRoster)
            {
                throw new InvalidOperationException("❌ Este jogador já está vinculado a um time.");
            }

            currentPick.PlayerId = player.PlayerId;
            currentPick.PickedAtUtc = DateTime.UtcNow;

            _db.TeamRosters.Add(new TeamRoster
            {
                TeamId = currentPick.TeamId,
                PlayerId = player.PlayerId
            });

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var state = await GetStateAsync(ct);

            try
            {
                await _hubContext.Clients.All.SendAsync("DraftAtualizado", cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar notificação de atualização do draft.");
            }

            return state;
        });
    }
}
