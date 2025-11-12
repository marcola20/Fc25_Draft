using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

public sealed class MatchService : IMatchService
{
    private readonly DraftDbContext _db;
    private readonly ITeamLineupService _teamLineupService;
    private readonly ILogger<MatchService> _logger;

    public MatchService(
        DraftDbContext db,
        ITeamLineupService teamLineupService,
        ILogger<MatchService> logger)
    {
        _db = db;
        _teamLineupService = teamLineupService;
        _logger = logger;
    }

    public async Task CaptureLineupsAsync(Guid matchId, CancellationToken ct)
    {
        if (matchId == Guid.Empty)
        {
            throw new ArgumentException("Partida inválida.", nameof(matchId));
        }

        var match = await _db.Matches
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct)
            .ConfigureAwait(false);

        if (match is null)
        {
            throw new KeyNotFoundException("Partida não encontrada.");
        }

        try
        {
            var homeLineup = await _teamLineupService.GetActiveAsync(match.HomeTeamId, ct).ConfigureAwait(false);
            var awayLineup = await _teamLineupService.GetActiveAsync(match.AwayTeamId, ct).ConfigureAwait(false);

            match.HomeLineupSnapshotJson = homeLineup is null ? null : JsonSerializer.Serialize(homeLineup);
            match.AwayLineupSnapshotJson = awayLineup is null ? null : JsonSerializer.Serialize(awayLineup);

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not KeyNotFoundException and not ArgumentException)
        {
            _logger.LogError(ex, "Erro ao capturar escalações da partida {MatchId}.", matchId);
            throw;
        }
    }
}
