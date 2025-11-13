using Fc25Draft.Core.DTOs.Competitions;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

public sealed class CompetitionService : ICompetitionService
{
    private readonly DraftDbContext _db;
    private readonly ILogger<CompetitionService> _logger;

    public CompetitionService(DraftDbContext db, ILogger<CompetitionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CompetitionSummaryDto>> GetCompetitionsAsync(CancellationToken ct)
    {
        var competitions = await _db.Competitions
            .AsNoTracking()
            .Select(c => new
            {
                c.CompetitionId,
                c.SeasonId,
                SeasonName = c.Season.Name,
                c.Name,
                c.Order,
                c.Type,
                c.IsActive,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                TeamCount = c.Teams.Count,
                RoundCount = c.Rounds.Count,
                MatchCount = c.Matches.Count
            })
            .OrderBy(c => c.SeasonName)
            .ThenBy(c => c.Order)
            .ThenBy(c => c.Name)
            .Select(c => new CompetitionSummaryDto(
                c.CompetitionId,
                c.SeasonId,
                c.SeasonName,
                c.Name,
                c.Order,
                c.Type,
                c.IsActive,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.TeamCount,
                c.RoundCount,
                c.MatchCount))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return competitions;
    }

    public async Task<CompetitionDetailsDto> GetCompetitionDetailsAsync(Guid competitionId, CancellationToken ct)
    {
        var competition = await _db.Competitions
            .AsNoTracking()
            .Include(c => c.Season)
            .FirstOrDefaultAsync(c => c.CompetitionId == competitionId, ct)
            .ConfigureAwait(false);

        if (competition is null)
        {
            throw new KeyNotFoundException("Competição não encontrada.");
        }

        var summary = new CompetitionSummaryDto(
            competition.CompetitionId,
            competition.SeasonId,
            competition.Season.Name,
            competition.Name,
            competition.Order,
            competition.Type,
            competition.IsActive,
            competition.CreatedAtUtc,
            competition.UpdatedAtUtc,
            await _db.CompetitionTeams.CountAsync(t => t.CompetitionId == competitionId, ct).ConfigureAwait(false),
            await _db.Rounds.CountAsync(r => r.CompetitionId == competitionId, ct).ConfigureAwait(false),
            await _db.CompetitionMatches.CountAsync(m => m.CompetitionId == competitionId, ct).ConfigureAwait(false));

        var teams = await GetTeamsAsyncInternal(competitionId, ct).ConfigureAwait(false);
        var rounds = await GetRoundsAsync(competitionId, ct).ConfigureAwait(false);
        var standings = await GetStandingsAsync(competitionId, ct).ConfigureAwait(false);
        var teamStats = await GetTeamStatsAsync(competitionId, ct).ConfigureAwait(false);
        var playerStats = await GetPlayerStatsAsync(competitionId, ct).ConfigureAwait(false);

        return new CompetitionDetailsDto(summary, teams, rounds, standings, teamStats, playerStats);
    }

    public async Task<CompetitionSummaryDto> CreateCompetitionAsync(CompetitionCreateCommand command, string? performedBy, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var season = await _db.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SeasonId == command.SeasonId, ct)
            .ConfigureAwait(false);

        if (season is null)
        {
            throw new KeyNotFoundException("Temporada não encontrada.");
        }

        var competition = new Competition
        {
            CompetitionId = Guid.NewGuid(),
            SeasonId = command.SeasonId,
            Name = command.Name.Trim(),
            Order = command.Order,
            Type = command.Type,
            IsActive = command.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        try
        {
            _db.Competitions.Add(competition);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await LogAsync(competition.CompetitionId, null, "CreateCompetition", performedBy, new { command.Name, command.Order, command.Type, command.IsActive }, ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Falha ao criar competição {Name}", command.Name);
            throw;
        }

        return new CompetitionSummaryDto(
            competition.CompetitionId,
            competition.SeasonId,
            season.Name,
            competition.Name,
            competition.Order,
            competition.Type,
            competition.IsActive,
            competition.CreatedAtUtc,
            competition.UpdatedAtUtc,
            0,
            0,
            0);
    }

    public async Task<CompetitionSummaryDto?> UpdateCompetitionAsync(Guid competitionId, CompetitionUpdateCommand command, string? performedBy, CancellationToken ct)
    {
        var competition = await _db.Competitions.FirstOrDefaultAsync(c => c.CompetitionId == competitionId, ct).ConfigureAwait(false);
        if (competition is null)
        {
            return null;
        }

        competition.Name = command.Name.Trim();
        competition.Order = command.Order;
        competition.Type = command.Type;
        competition.IsActive = command.IsActive;
        competition.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await LogAsync(competitionId, null, "UpdateCompetition", performedBy, new { command.Name, command.Order, command.Type, command.IsActive }, ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Falha ao atualizar competição {CompetitionId}", competitionId);
            throw;
        }

        var season = await _db.Seasons.AsNoTracking().FirstAsync(s => s.SeasonId == competition.SeasonId, ct).ConfigureAwait(false);
        var teamCount = await _db.CompetitionTeams.CountAsync(t => t.CompetitionId == competitionId, ct).ConfigureAwait(false);
        var roundCount = await _db.Rounds.CountAsync(r => r.CompetitionId == competitionId, ct).ConfigureAwait(false);
        var matchCount = await _db.CompetitionMatches.CountAsync(m => m.CompetitionId == competitionId, ct).ConfigureAwait(false);

        return new CompetitionSummaryDto(
            competition.CompetitionId,
            competition.SeasonId,
            season.Name,
            competition.Name,
            competition.Order,
            competition.Type,
            competition.IsActive,
            competition.CreatedAtUtc,
            competition.UpdatedAtUtc,
            teamCount,
            roundCount,
            matchCount);
    }

    public async Task<bool> SetCompetitionActiveAsync(Guid competitionId, bool isActive, string? performedBy, CancellationToken ct)
    {
        var competition = await _db.Competitions.FirstOrDefaultAsync(c => c.CompetitionId == competitionId, ct).ConfigureAwait(false);
        if (competition is null)
        {
            return false;
        }

        competition.IsActive = isActive;
        competition.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await LogAsync(competitionId, null, "ToggleCompetition", performedBy, new { competitionId, isActive }, ct).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<CompetitionTeamDto>> GetTeamsAsync(Guid competitionId, CancellationToken ct)
        => await GetTeamsAsyncInternal(competitionId, ct).ConfigureAwait(false);

    public async Task<CompetitionTeamDto> AddTeamAsync(Guid competitionId, CompetitionTeamAssignCommand command, string? performedBy, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var competition = await _db.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompetitionId == competitionId, ct)
            .ConfigureAwait(false);

        if (competition is null)
        {
            throw new KeyNotFoundException("Competição não encontrada.");
        }

        var team = await _db.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TeamId == command.TeamId, ct)
            .ConfigureAwait(false);

        if (team is null)
        {
            throw new KeyNotFoundException("Time não encontrado.");
        }

        var exists = await _db.CompetitionTeams
            .AsNoTracking()
            .AnyAsync(t => t.CompetitionId == competitionId && t.TeamId == command.TeamId, ct)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new InvalidOperationException("O time já participa desta competição.");
        }

        var competitionTeam = new CompetitionTeam
        {
            CompetitionTeamId = Guid.NewGuid(),
            CompetitionId = competitionId,
            TeamId = command.TeamId,
            InitialBudget = command.InitialBudget,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IsActive = true
        };

        _db.CompetitionTeams.Add(competitionTeam);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await LogAsync(competitionId, null, "AddTeam", performedBy, new { command.TeamId, command.InitialBudget, command.Notes }, ct).ConfigureAwait(false);

        return new CompetitionTeamDto(
            competitionTeam.CompetitionTeamId,
            competitionTeam.TeamId,
            team.TeamName,
            competitionTeam.IsActive,
            competitionTeam.InitialBudget,
            competitionTeam.Notes,
            competitionTeam.CreatedAtUtc,
            competitionTeam.UpdatedAtUtc);
    }

    public async Task<bool> RemoveTeamAsync(Guid competitionTeamId, string? performedBy, CancellationToken ct)
    {
        var team = await _db.CompetitionTeams.FirstOrDefaultAsync(t => t.CompetitionTeamId == competitionTeamId, ct).ConfigureAwait(false);
        if (team is null)
        {
            return false;
        }

        var competitionId = team.CompetitionId;
        _db.CompetitionTeams.Remove(team);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await LogAsync(competitionId, null, "RemoveTeam", performedBy, new { competitionTeamId }, ct).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<CompetitionRoundDto>> GenerateRoundsAsync(Guid competitionId, CompetitionRoundGenerationCommand command, string? performedBy, CancellationToken ct)
    {
        var teams = await _db.CompetitionTeams
            .AsNoTracking()
            .Where(t => t.CompetitionId == competitionId && t.IsActive)
            .Select(t => new
            {
                t.CompetitionTeamId,
                t.Team.TeamName,
                t.CreatedAtUtc
            })
            .OrderBy(t => t.CreatedAtUtc)
            .ThenBy(t => t.TeamName)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (teams.Count < 2)
        {
            throw new InvalidOperationException("É necessário ao menos dois times ativos para gerar rodadas.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var existingMatches = await _db.CompetitionMatches.Where(m => m.CompetitionId == competitionId).ToListAsync(ct).ConfigureAwait(false);
            if (existingMatches.Count > 0)
            {
                var matchIds = existingMatches.Select(m => m.CompetitionMatchId).ToList();
                var existingEvents = await _db.CompetitionMatchEvents.Where(e => matchIds.Contains(e.CompetitionMatchId)).ToListAsync(ct).ConfigureAwait(false);
                if (existingEvents.Count > 0)
                {
                    _db.CompetitionMatchEvents.RemoveRange(existingEvents);
                }

                _db.CompetitionMatches.RemoveRange(existingMatches);
            }

            var existingRounds = await _db.Rounds.Where(r => r.CompetitionId == competitionId).ToListAsync(ct).ConfigureAwait(false);
            if (existingRounds.Count > 0)
            {
                _db.Rounds.RemoveRange(existingRounds);
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            var teamIds = teams.Select(t => t.CompetitionTeamId).ToList();
            if (teamIds.Count % 2 != 0)
            {
                teamIds.Add(Guid.Empty);
            }

            var totalRounds = teamIds.Count - 1;
            var includeReturn = command.IncludeReturnLeg;
            var totalMatchWeeks = includeReturn ? totalRounds * 2 : totalRounds;
            var schedule = new List<(Guid home, Guid away)[]>();

            var rotation = teamIds.ToList();
            var half = rotation.Count / 2;
            for (int round = 0; round < totalRounds; round++)
            {
                var pairings = new List<(Guid home, Guid away)>();
                for (int i = 0; i < half; i++)
                {
                    var home = rotation[i];
                    var away = rotation[rotation.Count - 1 - i];
                    if (home == Guid.Empty || away == Guid.Empty)
                    {
                        continue;
                    }

                    var swap = (round + i) % 2 == 1;
                    pairings.Add(swap ? (away, home) : (home, away));
                }

                schedule.Add(pairings.ToArray());

                var last = rotation[^1];
                rotation.RemoveAt(rotation.Count - 1);
                rotation.Insert(1, last);
            }

            var now = DateTime.UtcNow;
            var roundsToPersist = new List<Round>();
            var matchesToPersist = new List<CompetitionMatch>();

            DateTime? currentRoundDate = command.FirstRoundDateUtc;
            var daysBetween = command.DaysBetweenRounds.GetValueOrDefault(7);

            int roundNumber = 1;
            foreach (var pairings in schedule)
            {
                var round = new Round
                {
                    RoundId = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    Name = $"Rodada {roundNumber}",
                    RoundNumber = roundNumber,
                    ScheduledAtUtc = currentRoundDate,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    IsCompleted = false
                };

                roundsToPersist.Add(round);

                foreach (var (home, away) in pairings)
                {
                    matchesToPersist.Add(new CompetitionMatch
                    {
                        CompetitionMatchId = Guid.NewGuid(),
                        CompetitionId = competitionId,
                        RoundId = round.RoundId,
                        HomeCompetitionTeamId = home,
                        AwayCompetitionTeamId = away,
                        MatchDateUtc = currentRoundDate,
                        Status = CompetitionMatchStatus.Scheduled,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
                }

                if (currentRoundDate.HasValue)
                {
                    currentRoundDate = currentRoundDate.Value.AddDays(daysBetween);
                }

                roundNumber++;
            }

            if (includeReturn)
            {
                foreach (var pairings in schedule)
                {
                    var round = new Round
                    {
                        RoundId = Guid.NewGuid(),
                        CompetitionId = competitionId,
                        Name = $"Rodada {roundNumber}",
                        RoundNumber = roundNumber,
                        ScheduledAtUtc = currentRoundDate,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now,
                        IsCompleted = false
                    };

                    roundsToPersist.Add(round);

                    foreach (var (home, away) in pairings)
                    {
                        matchesToPersist.Add(new CompetitionMatch
                        {
                            CompetitionMatchId = Guid.NewGuid(),
                            CompetitionId = competitionId,
                            RoundId = round.RoundId,
                            HomeCompetitionTeamId = away,
                            AwayCompetitionTeamId = home,
                            MatchDateUtc = currentRoundDate,
                            Status = CompetitionMatchStatus.Scheduled,
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now
                        });
                    }

                    if (currentRoundDate.HasValue)
                    {
                        currentRoundDate = currentRoundDate.Value.AddDays(daysBetween);
                    }

                    roundNumber++;
                }
            }

            _db.Rounds.AddRange(roundsToPersist);
            _db.CompetitionMatches.AddRange(matchesToPersist);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            await LogAsync(competitionId, null, "GenerateRounds", performedBy, new { command.IncludeReturnLeg, command.FirstRoundDateUtc, command.DaysBetweenRounds }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogError(ex, "Falha ao gerar rodadas para a competição {CompetitionId}", competitionId);
            throw;
        }

        return await GetRoundsAsync(competitionId, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CompetitionRoundDto>> GetRoundsAsync(Guid competitionId, CancellationToken ct)
    {
        var rounds = await _db.Rounds
            .AsNoTracking()
            .Where(r => r.CompetitionId == competitionId)
            .OrderBy(r => r.RoundNumber)
            .Select(r => new CompetitionRoundDto(
                r.RoundId,
                r.RoundNumber,
                r.Name,
                r.ScheduledAtUtc,
                r.IsCompleted,
                r.PlayedAtUtc,
                new List<CompetitionMatchDto>()))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var roundIds = rounds.Select(r => r.RoundId).ToList();
        if (roundIds.Count == 0)
        {
            return rounds;
        }

        var matches = await _db.CompetitionMatches
            .AsNoTracking()
            .Where(m => roundIds.Contains(m.RoundId))
            .Select(m => new
            {
                m.CompetitionMatchId,
                m.CompetitionId,
                m.RoundId,
                m.HomeCompetitionTeamId,
                HomeName = m.HomeTeam.Team.TeamName,
                m.AwayCompetitionTeamId,
                AwayName = m.AwayTeam.Team.TeamName,
                m.MatchDateUtc,
                m.HomeGoals,
                m.AwayGoals,
                m.Status,
                m.Stadium,
                m.Observations
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var matchLookup = matches.GroupBy(m => m.RoundId).ToDictionary(g => g.Key, g => g.Select(m => new CompetitionMatchDto(
            m.CompetitionMatchId,
            m.CompetitionId,
            m.RoundId,
            m.HomeCompetitionTeamId,
            m.HomeName,
            m.AwayCompetitionTeamId,
            m.AwayName,
            m.MatchDateUtc,
            m.HomeGoals,
            m.AwayGoals,
            m.Status,
            m.Stadium,
            m.Observations)).OrderBy(m => m.MatchDateUtc ?? DateTime.MaxValue).ToList() as IReadOnlyList<CompetitionMatchDto>);

        var result = rounds.Select(r => r with
        {
            Matches = matchLookup.TryGetValue(r.RoundId, out var list) ? list : Array.Empty<CompetitionMatchDto>()
        }).ToList();

        return result;
    }

    public async Task<CompetitionMatchDetailsDto> UpsertMatchAsync(CompetitionMatchUpsertCommand command, string? performedBy, CancellationToken ct)
    {
        var match = await _db.CompetitionMatches
            .Include(m => m.HomeTeam).ThenInclude(t => t.Team)
            .Include(m => m.AwayTeam).ThenInclude(t => t.Team)
            .FirstOrDefaultAsync(m => m.CompetitionMatchId == command.CompetitionMatchId, ct)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;

        if (match is null)
        {
            match = new CompetitionMatch
            {
                CompetitionMatchId = command.CompetitionMatchId == Guid.Empty ? Guid.NewGuid() : command.CompetitionMatchId,
                CompetitionId = command.CompetitionId,
                RoundId = command.RoundId,
                HomeCompetitionTeamId = command.HomeCompetitionTeamId,
                AwayCompetitionTeamId = command.AwayCompetitionTeamId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Status = command.Status,
                MatchDateUtc = command.MatchDateUtc,
                HomeGoals = command.HomeGoals,
                AwayGoals = command.AwayGoals,
                Stadium = string.IsNullOrWhiteSpace(command.Stadium) ? null : command.Stadium.Trim(),
                Observations = string.IsNullOrWhiteSpace(command.Observations) ? null : command.Observations.Trim()
            };

            _db.CompetitionMatches.Add(match);
        }
        else
        {
            match.MatchDateUtc = command.MatchDateUtc;
            match.Status = command.Status;
            match.HomeGoals = command.HomeGoals;
            match.AwayGoals = command.AwayGoals;
            match.Stadium = string.IsNullOrWhiteSpace(command.Stadium) ? null : command.Stadium.Trim();
            match.Observations = string.IsNullOrWhiteSpace(command.Observations) ? null : command.Observations.Trim();
            match.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await LogAsync(match.CompetitionId, match.CompetitionMatchId, "UpsertMatch", performedBy, new { command }, ct).ConfigureAwait(false);

        if (match.Status == CompetitionMatchStatus.Finished)
        {
            await RebuildStandingsAsync(match.CompetitionId, performedBy, ct).ConfigureAwait(false);
        }

        return await GetMatchDetailsAsync(match.CompetitionMatchId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Partida não encontrada após atualização.");
    }

    public async Task<bool> DeleteMatchAsync(Guid competitionMatchId, string? performedBy, CancellationToken ct)
    {
        var match = await _db.CompetitionMatches.FirstOrDefaultAsync(m => m.CompetitionMatchId == competitionMatchId, ct).ConfigureAwait(false);
        if (match is null)
        {
            return false;
        }

        var events = await _db.CompetitionMatchEvents.Where(e => e.CompetitionMatchId == competitionMatchId).ToListAsync(ct).ConfigureAwait(false);
        if (events.Count > 0)
        {
            _db.CompetitionMatchEvents.RemoveRange(events);
        }

        var competitionId = match.CompetitionId;
        _db.CompetitionMatches.Remove(match);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await LogAsync(competitionId, competitionMatchId, "DeleteMatch", performedBy, new { competitionMatchId }, ct).ConfigureAwait(false);
        await RebuildStandingsAsync(competitionId, performedBy, ct).ConfigureAwait(false);
        return true;
    }

    public async Task<CompetitionMatchDetailsDto?> GetMatchDetailsAsync(Guid competitionMatchId, CancellationToken ct)
    {
        var match = await _db.CompetitionMatches
            .AsNoTracking()
            .Where(m => m.CompetitionMatchId == competitionMatchId)
            .Select(m => new
            {
                m.CompetitionMatchId,
                m.CompetitionId,
                m.RoundId,
                m.HomeCompetitionTeamId,
                HomeName = m.HomeTeam.Team.TeamName,
                m.AwayCompetitionTeamId,
                AwayName = m.AwayTeam.Team.TeamName,
                m.MatchDateUtc,
                m.HomeGoals,
                m.AwayGoals,
                m.Status,
                m.Stadium,
                m.Observations
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (match is null)
        {
            return null;
        }

        var events = await _db.CompetitionMatchEvents
            .AsNoTracking()
            .Where(e => e.CompetitionMatchId == competitionMatchId)
            .OrderBy(e => e.Minute ?? 0)
            .ThenBy(e => e.CreatedAtUtc)
            .Select(e => new CompetitionMatchEventDto(
                e.CompetitionMatchEventId,
                e.CompetitionTeamId,
                e.Team.Team.TeamName,
                e.PlayerId,
                e.Player != null ? e.Player.Name : null,
                e.RelatedPlayerId,
                e.RelatedPlayer != null ? e.RelatedPlayer.Name : null,
                e.EventType,
                e.Minute,
                e.Observations))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new CompetitionMatchDetailsDto(
            match.CompetitionMatchId,
            match.CompetitionId,
            match.RoundId,
            match.HomeCompetitionTeamId,
            match.HomeName,
            match.AwayCompetitionTeamId,
            match.AwayName,
            match.MatchDateUtc,
            match.HomeGoals,
            match.AwayGoals,
            match.Status,
            match.Stadium,
            match.Observations,
            events);
    }

    public async Task<CompetitionMatchDetailsDto> ReplaceMatchEventsAsync(Guid competitionMatchId, IReadOnlyCollection<CompetitionMatchEventUpsertCommand> events, string? performedBy, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var match = await _db.CompetitionMatches.FirstOrDefaultAsync(m => m.CompetitionMatchId == competitionMatchId, ct).ConfigureAwait(false);
            if (match is null)
            {
                throw new KeyNotFoundException("Partida não encontrada.");
            }

            var existing = await _db.CompetitionMatchEvents.Where(e => e.CompetitionMatchId == competitionMatchId).ToListAsync(ct).ConfigureAwait(false);
            if (existing.Count > 0)
            {
                _db.CompetitionMatchEvents.RemoveRange(existing);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            var now = DateTime.UtcNow;
            foreach (var evt in events)
            {
                var entity = new CompetitionMatchEvent
                {
                    CompetitionMatchEventId = evt.CompetitionMatchEventId.GetValueOrDefault(Guid.NewGuid()),
                    CompetitionMatchId = competitionMatchId,
                    CompetitionTeamId = evt.CompetitionTeamId,
                    PlayerId = evt.PlayerId,
                    RelatedPlayerId = evt.RelatedPlayerId,
                    EventType = evt.EventType,
                    Minute = evt.Minute,
                    Observations = string.IsNullOrWhiteSpace(evt.Observations) ? null : evt.Observations.Trim(),
                    CreatedAtUtc = now
                };

                _db.CompetitionMatchEvents.Add(entity);
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            await LogAsync(match.CompetitionId, competitionMatchId, "ReplaceMatchEvents", performedBy, new { eventsCount = events.Count }, ct).ConfigureAwait(false);
            await RebuildStandingsAsync(match.CompetitionId, performedBy, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogError(ex, "Erro ao atualizar eventos da partida {MatchId}", competitionMatchId);
            throw;
        }

        return await GetMatchDetailsAsync(competitionMatchId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Partida não encontrada após atualizar eventos.");
    }

    public async Task<IReadOnlyList<CompetitionStandingDto>> GetStandingsAsync(Guid competitionId, CancellationToken ct)
    {
        var standings = await _db.CompetitionStandings
            .AsNoTracking()
            .Where(s => s.CompetitionId == competitionId)
            .OrderBy(s => s.Position)
            .Select(s => new CompetitionStandingDto(
                s.CompetitionStandingId,
                s.CompetitionTeamId,
                s.CompetitionTeam.TeamId,
                s.CompetitionTeam.Team.TeamName,
                s.Position,
                s.MatchesPlayed,
                s.Wins,
                s.Draws,
                s.Losses,
                s.GoalsFor,
                s.GoalsAgainst,
                s.GoalDifference,
                s.Points,
                s.YellowCards,
                s.RedCards,
                s.UpdatedAtUtc))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return standings;
    }

    public async Task<IReadOnlyList<CompetitionStandingDto>> RebuildStandingsAsync(Guid competitionId, string? performedBy, CancellationToken ct)
    {
        var teams = await _db.CompetitionTeams
            .AsNoTracking()
            .Where(t => t.CompetitionId == competitionId)
            .Select(t => new
            {
                t.CompetitionTeamId,
                t.TeamId,
                t.Team.TeamName,
                t.IsActive
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var matches = await _db.CompetitionMatches
            .AsNoTracking()
            .Where(m => m.CompetitionId == competitionId && m.Status == CompetitionMatchStatus.Finished)
            .Select(m => new
            {
                m.CompetitionMatchId,
                m.HomeCompetitionTeamId,
                m.AwayCompetitionTeamId,
                HomeGoals = m.HomeGoals ?? 0,
                AwayGoals = m.AwayGoals ?? 0
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var teamMap = teams.ToDictionary(t => t.CompetitionTeamId, t => new StandingAccumulator(t.CompetitionTeamId, t.TeamId, t.TeamName));

        foreach (var match in matches)
        {
            if (!teamMap.TryGetValue(match.HomeCompetitionTeamId, out var home) || !teamMap.TryGetValue(match.AwayCompetitionTeamId, out var away))
            {
                continue;
            }

            home.MatchesPlayed++;
            away.MatchesPlayed++;

            home.GoalsFor += match.HomeGoals;
            home.GoalsAgainst += match.AwayGoals;
            away.GoalsFor += match.AwayGoals;
            away.GoalsAgainst += match.HomeGoals;

            if (match.HomeGoals > match.AwayGoals)
            {
                home.Wins++;
                home.Points += 3;
                away.Losses++;
            }
            else if (match.HomeGoals < match.AwayGoals)
            {
                away.Wins++;
                away.Points += 3;
                home.Losses++;
            }
            else
            {
                home.Draws++;
                away.Draws++;
                home.Points++;
                away.Points++;
            }
        }

        var events = await _db.CompetitionMatchEvents
            .AsNoTracking()
            .Where(e => e.Match.CompetitionId == competitionId)
            .Select(e => new MatchEventProjection(
                e.CompetitionMatchId,
                e.CompetitionTeamId,
                e.PlayerId,
                e.EventType))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var evt in events)
        {
            if (teamMap.TryGetValue(evt.CompetitionTeamId, out var team))
            {
                if (evt.EventType == CompetitionMatchEventType.YellowCard)
                {
                    team.YellowCards++;
                }
                else if (evt.EventType == CompetitionMatchEventType.RedCard)
                {
                    team.RedCards++;
                }
            }
        }

        var standings = teamMap.Values
            .Select(s => s.ToStanding())
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ThenBy(s => s.TeamName)
            .Select((s, index) => s with { Position = index + 1 })
            .ToList();

        var now = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var existing = await _db.CompetitionStandings.Where(s => s.CompetitionId == competitionId).ToListAsync(ct).ConfigureAwait(false);
            if (existing.Count > 0)
            {
                _db.CompetitionStandings.RemoveRange(existing);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            foreach (var standing in standings)
            {
                _db.CompetitionStandings.Add(new CompetitionStanding
                {
                    CompetitionStandingId = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    CompetitionTeamId = standing.CompetitionTeamId,
                    Position = standing.Position,
                    MatchesPlayed = standing.MatchesPlayed,
                    Wins = standing.Wins,
                    Draws = standing.Draws,
                    Losses = standing.Losses,
                    GoalsFor = standing.GoalsFor,
                    GoalsAgainst = standing.GoalsAgainst,
                    GoalDifference = standing.GoalDifference,
                    Points = standing.Points,
                    YellowCards = standing.YellowCards,
                    RedCards = standing.RedCards,
                    UpdatedAtUtc = now
                });
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            await UpdateTeamStatsAsync(competitionId, teamMap.Values.ToDictionary(v => v.CompetitionTeamId, v => v), ct).ConfigureAwait(false);
            await UpdatePlayerStatsAsync(competitionId, events, ct).ConfigureAwait(false);

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogError(ex, "Falha ao recalcular classificação da competição {CompetitionId}", competitionId);
            throw;
        }

        await LogAsync(competitionId, null, "RebuildStandings", performedBy, new { competitionId }, ct).ConfigureAwait(false);
        return await GetStandingsAsync(competitionId, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CompetitionPlayerStatDto>> GetPlayerStatsAsync(Guid competitionId, CancellationToken ct)
    {
        var stats = await _db.CompetitionPlayerStats
            .AsNoTracking()
            .Where(s => s.CompetitionId == competitionId)
            .OrderByDescending(s => s.Goals)
            .ThenByDescending(s => s.Assists)
            .ThenByDescending(s => s.MatchesPlayed)
            .Select(s => new CompetitionPlayerStatDto(
                s.CompetitionPlayerStatId,
                s.CompetitionTeamId,
                s.CompetitionTeam.TeamId,
                s.CompetitionTeam.Team.TeamName,
                s.PlayerId,
                s.Player.Name,
                s.MatchesPlayed,
                s.Goals,
                s.Assists,
                s.YellowCards,
                s.RedCards))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return stats;
    }

    public async Task<IReadOnlyList<CompetitionTeamStatDto>> GetTeamStatsAsync(Guid competitionId, CancellationToken ct)
    {
        var stats = await _db.CompetitionTeamStats
            .AsNoTracking()
            .Where(s => s.CompetitionId == competitionId)
            .OrderByDescending(s => s.GoalsFor)
            .ThenBy(s => s.GoalsAgainst)
            .Select(s => new CompetitionTeamStatDto(
                s.CompetitionTeamStatId,
                s.CompetitionTeamId,
                s.CompetitionTeam.TeamId,
                s.CompetitionTeam.Team.TeamName,
                s.MatchesPlayed,
                s.Wins,
                s.Draws,
                s.Losses,
                s.GoalsFor,
                s.GoalsAgainst,
                s.GoalDifference,
                s.YellowCards,
                s.RedCards))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return stats;
    }

    private async Task<IReadOnlyList<CompetitionTeamDto>> GetTeamsAsyncInternal(Guid competitionId, CancellationToken ct)
    {
        var teams = await _db.CompetitionTeams
            .AsNoTracking()
            .Where(t => t.CompetitionId == competitionId)
            .OrderByDescending(t => t.IsActive)
            .ThenBy(t => t.Team.TeamName)
            .Select(t => new CompetitionTeamDto(
                t.CompetitionTeamId,
                t.TeamId,
                t.Team.TeamName,
                t.IsActive,
                t.InitialBudget,
                t.Notes,
                t.CreatedAtUtc,
                t.UpdatedAtUtc))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return teams;
    }

    private async Task LogAsync(Guid? competitionId, Guid? matchId, string action, string? performedBy, object? details, CancellationToken ct)
    {
        try
        {
            var log = new CompetitionLog
            {
                CompetitionLogId = Guid.NewGuid(),
                CompetitionId = competitionId,
                CompetitionMatchId = matchId,
                Action = action,
                PerformedBy = string.IsNullOrWhiteSpace(performedBy) ? null : performedBy,
                Details = details is null ? null : System.Text.Json.JsonSerializer.Serialize(details),
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.CompetitionLogs.Add(log);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar log da competição {CompetitionId} - {Action}", competitionId, action);
        }
    }

    private async Task UpdateTeamStatsAsync(Guid competitionId, IDictionary<Guid, StandingAccumulator> standings, CancellationToken ct)
    {
        var existing = await _db.CompetitionTeamStats.Where(s => s.CompetitionId == competitionId).ToListAsync(ct).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            _db.CompetitionTeamStats.RemoveRange(existing);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        foreach (var accumulator in standings.Values)
        {
            _db.CompetitionTeamStats.Add(new CompetitionTeamStat
            {
                CompetitionTeamStatId = Guid.NewGuid(),
                CompetitionId = competitionId,
                CompetitionTeamId = accumulator.CompetitionTeamId,
                MatchesPlayed = accumulator.MatchesPlayed,
                Wins = accumulator.Wins,
                Draws = accumulator.Draws,
                Losses = accumulator.Losses,
                GoalsFor = accumulator.GoalsFor,
                GoalsAgainst = accumulator.GoalsAgainst,
                GoalDifference = accumulator.GoalDifference,
                YellowCards = accumulator.YellowCards,
                RedCards = accumulator.RedCards
            });
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task UpdatePlayerStatsAsync(Guid competitionId, IReadOnlyCollection<MatchEventProjection> events, CancellationToken ct)
    {
        var existing = await _db.CompetitionPlayerStats.Where(s => s.CompetitionId == competitionId).ToListAsync(ct).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            _db.CompetitionPlayerStats.RemoveRange(existing);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        var grouped = events
            .Where(e => e.PlayerId.HasValue)
            .GroupBy(e => new { e.CompetitionTeamId, PlayerId = e.PlayerId!.Value })
            .Select(g => new CompetitionPlayerStat
            {
                CompetitionPlayerStatId = Guid.NewGuid(),
                CompetitionId = competitionId,
                CompetitionTeamId = g.Key.CompetitionTeamId,
                PlayerId = g.Key.PlayerId,
                MatchesPlayed = g.Select(ev => ev.CompetitionMatchId).Distinct().Count(),
                Goals = g.Count(ev => ev.EventType == CompetitionMatchEventType.Goal),
                Assists = g.Count(ev => ev.EventType == CompetitionMatchEventType.Assist),
                YellowCards = g.Count(ev => ev.EventType == CompetitionMatchEventType.YellowCard),
                RedCards = g.Count(ev => ev.EventType == CompetitionMatchEventType.RedCard)
            })
            .ToList();

        if (grouped.Count > 0)
        {
            _db.CompetitionPlayerStats.AddRange(grouped);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    private sealed record MatchEventProjection(
        Guid CompetitionMatchId,
        Guid CompetitionTeamId,
        int? PlayerId,
        CompetitionMatchEventType EventType);

    private sealed record StandingAccumulator(Guid CompetitionTeamId, Guid TeamId, string TeamName)
    {
        public int MatchesPlayed { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public int GoalDifference => GoalsFor - GoalsAgainst;

        public CompetitionStandingDto ToStanding() => new(
            Guid.Empty,
            CompetitionTeamId,
            TeamId,
            TeamName,
            0,
            MatchesPlayed,
            Wins,
            Draws,
            Losses,
            GoalsFor,
            GoalsAgainst,
            GoalDifference,
            Points,
            YellowCards,
            RedCards,
            DateTime.UtcNow);
    }
}
