using System;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Hubs;
using Fc25Draft.Web.Security;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Web.Services;

public class DraftStateService
{
    private readonly DraftDbContext _db;
    private readonly IHubContext<DraftHub> _hubContext;
    private readonly ILogger<DraftStateService> _logger;
    private readonly SecurityOptions _securityOptions;

    public DraftStateService(
        DraftDbContext db,
        IHubContext<DraftHub> hubContext,
        ILogger<DraftStateService> logger,
        IOptions<SecurityOptions> securityOptions)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
        _securityOptions = securityOptions.Value;
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

    public async Task<IReadOnlyList<AvailablePlayerDto>> GetAvailablePlayersAsync(
        short? positionId,
        string? searchTerm = null,
        int? overallMinFilter = null,
        int? overallMaxFilter = null,
        CancellationToken ct = default)
    {
        var draft = await _db.Drafts
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (draft is null)
        {
            return Array.Empty<AvailablePlayerDto>();
        }

        var currentRoundNumber = await _db.DraftPicks
            .Where(p => p.DraftId == draft.DraftId && p.PlayerId == null)
            .OrderBy(p => p.OverallPick)
            .Select(p => (int?)p.RoundNumber)
            .FirstOrDefaultAsync(ct);

        if (!currentRoundNumber.HasValue)
        {
            return Array.Empty<AvailablePlayerDto>();
        }

        var currentRound = await _db.DraftRounds
            .AsNoTracking()
            .Where(r => r.DraftId == draft.DraftId && r.RoundNumber == currentRoundNumber.Value)
            .Select(r => new { r.OverallMin, r.OverallMax })
            .FirstOrDefaultAsync(ct);

        var query = _db.Players
            .AsNoTracking()
            .Include(p => p.Position)
            .Where(p => !p.TeamRosters.Any());

        if (positionId.HasValue)
        {
            query = query.Where(p => p.PositionId == positionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern));
        }

        if (currentRound?.OverallMin is int overallMin)
        {
            query = query.Where(p => p.Overall >= overallMin);
        }

        if (currentRound?.OverallMax is int overallMax)
        {
            query = query.Where(p => p.Overall <= overallMax);
        }

        if (overallMinFilter.HasValue)
        {
            query = query.Where(p => p.Overall >= overallMinFilter.Value);
        }

        if (overallMaxFilter.HasValue)
        {
            query = query.Where(p => p.Overall <= overallMaxFilter.Value);
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

    public async Task<DraftPickResultDto> MakePickAsync(int playerId, string token, CancellationToken ct = default)
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

            var roundLimits = await _db.DraftRounds
                .AsNoTracking()
                .Where(r => r.DraftId == draft.DraftId && r.RoundNumber == currentPick.RoundNumber)
                .Select(r => new { r.OverallMin, r.OverallMax })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Não foi possível localizar as regras da rodada atual.");

            if (!string.Equals(normalizedToken, currentPick.Team.Token, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("⚠️ Token inválido para este time.");
            }

            var player = await _db.Players
                .Include(p => p.Position)
                .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct)
                ?? throw new InvalidOperationException("Jogador não encontrado.");

            if (roundLimits.OverallMin is int overallMin && player.Overall < overallMin)
            {
                throw new InvalidOperationException($"❌ Este jogador está abaixo do overall mínimo ({overallMin}) permitido nesta rodada.");
            }

            if (roundLimits.OverallMax is int overallMax && player.Overall > overallMax)
            {
                throw new InvalidOperationException($"❌ Este jogador excede o overall máximo ({overallMax}) permitido nesta rodada.");
            }

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

            var selection = BuildSelectionInfo(currentPick, player, state);

            try
            {
                await _hubContext.Clients.All.SendAsync("DraftAtualizado", cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar notificação de atualização do draft.");
            }

            return new DraftPickResultDto(state, selection);
        });
    }

    private DraftPickSelectionDto BuildSelectionInfo(DraftPick pick, Player player, DraftStateDto state)
    {
        var nextTeamName = state.DraftCompleted
            ? "Draft concluído"
            : string.IsNullOrWhiteSpace(state.CurrentTeamName)
                ? "A definir"
                : state.CurrentTeamName!;

        var message = BuildWhatsappMessage(pick.Team.TeamName, player.Name, pick.PickInRound, pick.RoundNumber, nextTeamName);
        var shareUrl = BuildWhatsappUrl(pick.Team.TeamName, player.Name, pick.PickInRound, pick.RoundNumber, nextTeamName);

        var groupLink = string.IsNullOrWhiteSpace(_securityOptions.WhatsappGroupLink)
            ? null
            : _securityOptions.WhatsappGroupLink;

        return new DraftPickSelectionDto(
            pick.DraftId,
            pick.RoundNumber,
            pick.PickInRound,
            pick.OverallPick,
            pick.TeamId,
            pick.Team.TeamName,
            pick.Team.OwnerName,
            player.PlayerId,
            player.Name,
            player.PositionId,
            player.Position.Name,
            message,
            shareUrl,
            nextTeamName,
            groupLink);
    }

    private static string BuildWhatsappMessage(string team, string player, int pick, int round, string nextTeam)
        => $"O time {team} escolheu {player} com a escolha {pick} da rodada {round}! Próximo a escolher: {nextTeam}.";

    public static string BuildWhatsappUrl(string team, string player, int pick, int round, string nextTeam)
    {
        var message = BuildWhatsappMessage(team, player, pick, round, nextTeam);
        return $"https://wa.me/?text={Uri.EscapeDataString(message)}";
    }
}
