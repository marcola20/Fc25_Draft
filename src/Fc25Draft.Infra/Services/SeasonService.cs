using Fc25Draft.Core.DTOs.Seasons;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public sealed class SeasonService : ISeasonQueryService, ISeasonAdminService
{
    private readonly DraftDbContext _db;

    public SeasonService(DraftDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(CancellationToken ct)
    {
        var seasons = await _db.Seasons
            .AsNoTracking()
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name)
            .Select(s => new SeasonDto(s.SeasonId, s.Name, s.IsActive))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return seasons;
    }

    public async Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(Guid seasonId, CancellationToken ct)
    {
        var exists = await _db.Seasons
            .AsNoTracking()
            .AnyAsync(s => s.SeasonId == seasonId, ct)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new KeyNotFoundException("Temporada não encontrada.");
        }

        var competitions = await _db.Competitions
            .AsNoTracking()
            .Where(c => c.SeasonId == seasonId)
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Name)
            .Select(c => new CompetitionDto(c.CompetitionId, c.SeasonId, c.Name, c.Order, c.IsActive))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return competitions;
    }

    public async Task<IReadOnlyList<RoundDto>> GetRoundsAsync(Guid competitionId, CancellationToken ct)
    {
        var exists = await _db.Competitions
            .AsNoTracking()
            .AnyAsync(c => c.CompetitionId == competitionId, ct)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new KeyNotFoundException("Competição não encontrada.");
        }

        var rounds = await _db.Rounds
            .AsNoTracking()
            .Where(r => r.CompetitionId == competitionId)
            .OrderBy(r => r.RoundId)
            .Select(r => new RoundDto(r.RoundId, r.CompetitionId, r.Name, r.IsCompleted, r.PlayedAtUtc, r.Notes))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rounds;
    }

    public async Task<IReadOnlyList<SeasonScheduleEntryDto>> GetScheduleAsync(Guid seasonId, CancellationToken ct)
    {
        var schedule = await _db.SeasonSchedule
            .AsNoTracking()
            .Where(s => s.SeasonId == seasonId)
            .OrderBy(s => s.Order)
            .Select(s => new SeasonScheduleEntryDto(
                s.SeasonScheduleItemId,
                s.SeasonId,
                s.Order,
                s.Round.CompetitionId,
                s.Round.Competition.Name,
                s.RoundId,
                s.Round.Name,
                s.Round.IsCompleted,
                s.Round.PlayedAtUtc,
                s.Round.Notes))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (schedule.Count == 0)
        {
            var exists = await _db.Seasons.AsNoTracking().AnyAsync(s => s.SeasonId == seasonId, ct).ConfigureAwait(false);
            if (!exists)
            {
                throw new KeyNotFoundException("Temporada não encontrada.");
            }
        }

        return schedule;
    }

    public async Task<SeasonDto> CreateSeasonAsync(SeasonUpsertCommand command, CancellationToken ct)
    {
        var season = new Season
        {
            SeasonId = Guid.NewGuid(),
            Name = command.Name.Trim(),
            IsActive = command.IsActive
        };

        _db.Seasons.Add(season);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new SeasonDto(season.SeasonId, season.Name, season.IsActive);
    }

    public async Task<SeasonDto?> UpdateSeasonAsync(Guid seasonId, SeasonUpsertCommand command, CancellationToken ct)
    {
        var season = await _db.Seasons.FirstOrDefaultAsync(s => s.SeasonId == seasonId, ct).ConfigureAwait(false);
        if (season is null)
        {
            return null;
        }

        season.Name = command.Name.Trim();
        season.IsActive = command.IsActive;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new SeasonDto(season.SeasonId, season.Name, season.IsActive);
    }

    public async Task<bool> DeleteSeasonAsync(Guid seasonId, CancellationToken ct)
    {
        var season = await _db.Seasons.FirstOrDefaultAsync(s => s.SeasonId == seasonId, ct).ConfigureAwait(false);
        if (season is null)
        {
            return false;
        }

        _db.Seasons.Remove(season);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<CompetitionDto> CreateCompetitionAsync(Guid seasonId, CompetitionUpsertCommand command, CancellationToken ct)
    {
        var seasonExists = await _db.Seasons
            .AsNoTracking()
            .AnyAsync(s => s.SeasonId == seasonId, ct)
            .ConfigureAwait(false);

        if (!seasonExists)
        {
            throw new KeyNotFoundException("Temporada não encontrada.");
        }

        var now = DateTime.UtcNow;

        var competition = new Competition
        {
            CompetitionId = Guid.NewGuid(),
            SeasonId = seasonId,
            Name = command.Name.Trim(),
            Order = command.Order,
            IsActive = command.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Competitions.Add(competition);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new CompetitionDto(competition.CompetitionId, competition.SeasonId, competition.Name, competition.Order, competition.IsActive);
    }

    public async Task<CompetitionDto?> UpdateCompetitionAsync(Guid competitionId, CompetitionUpsertCommand command, CancellationToken ct)
    {
        var competition = await _db.Competitions.FirstOrDefaultAsync(c => c.CompetitionId == competitionId, ct).ConfigureAwait(false);
        if (competition is null)
        {
            return null;
        }

        competition.Name = command.Name.Trim();
        competition.Order = command.Order;
        competition.IsActive = command.IsActive;
        competition.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new CompetitionDto(competition.CompetitionId, competition.SeasonId, competition.Name, competition.Order, competition.IsActive);
    }

    public async Task<bool> DeleteCompetitionAsync(Guid competitionId, CancellationToken ct)
    {
        var competition = await _db.Competitions.FirstOrDefaultAsync(c => c.CompetitionId == competitionId, ct).ConfigureAwait(false);
        if (competition is null)
        {
            return false;
        }

        _db.Competitions.Remove(competition);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<RoundDto> CreateRoundAsync(Guid competitionId, RoundUpsertCommand command, CancellationToken ct)
    {
        var competitionExists = await _db.Competitions
            .AsNoTracking()
            .AnyAsync(c => c.CompetitionId == competitionId, ct)
            .ConfigureAwait(false);

        if (!competitionExists)
        {
            throw new KeyNotFoundException("Competição não encontrada.");
        }

        var now = DateTime.UtcNow;
        var nextNumber = await _db.Rounds
            .AsNoTracking()
            .Where(r => r.CompetitionId == competitionId)
            .Select(r => (int?)r.RoundNumber)
            .MaxAsync(ct)
            .ConfigureAwait(false) ?? 0;

        var round = new Round
        {
            RoundId = Guid.NewGuid(),
            CompetitionId = competitionId,
            Name = command.Name.Trim(),
            IsCompleted = command.IsCompleted,
            PlayedAtUtc = command.PlayedAtUtc,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            RoundNumber = nextNumber + 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Rounds.Add(round);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new RoundDto(round.RoundId, round.CompetitionId, round.Name, round.IsCompleted, round.PlayedAtUtc, round.Notes);
    }

    public async Task<RoundDto?> UpdateRoundAsync(Guid roundId, RoundUpsertCommand command, CancellationToken ct)
    {
        var round = await _db.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId, ct).ConfigureAwait(false);
        if (round is null)
        {
            return null;
        }

        round.Name = command.Name.Trim();
        round.IsCompleted = command.IsCompleted;
        round.PlayedAtUtc = command.PlayedAtUtc;
        round.Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim();
        round.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new RoundDto(round.RoundId, round.CompetitionId, round.Name, round.IsCompleted, round.PlayedAtUtc, round.Notes);
    }

    public async Task<RoundDto?> UpdateRoundCompletionAsync(Guid roundId, RoundCompletionCommand command, CancellationToken ct)
    {
        var round = await _db.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId, ct).ConfigureAwait(false);
        if (round is null)
        {
            return null;
        }

        round.IsCompleted = command.IsCompleted;
        round.PlayedAtUtc = command.PlayedAtUtc;
        round.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new RoundDto(round.RoundId, round.CompetitionId, round.Name, round.IsCompleted, round.PlayedAtUtc, round.Notes);
    }

    public async Task<bool> DeleteRoundAsync(Guid roundId, CancellationToken ct)
    {
        var round = await _db.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId, ct).ConfigureAwait(false);
        if (round is null)
        {
            return false;
        }

        _db.Rounds.Remove(round);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<SeasonScheduleEntryDto>> UpdateSeasonScheduleAsync(SeasonScheduleUpdateCommand command, CancellationToken ct)
    {
        var season = await _db.Seasons.FirstOrDefaultAsync(s => s.SeasonId == command.SeasonId, ct).ConfigureAwait(false);
        if (season is null)
        {
            throw new KeyNotFoundException("Temporada não encontrada.");
        }

        if (command.Items.Count == 0)
        {
            throw new InvalidOperationException("Informe ao menos uma rodada para o calendário.");
        }

        if (command.Items.Select(i => i.Order).Distinct().Count() != command.Items.Count)
        {
            throw new InvalidOperationException("Não é permitido repetir a ordem do calendário.");
        }

        var roundIds = command.Items.Select(i => i.RoundId).ToList();
        if (roundIds.Distinct().Count() != roundIds.Count)
        {
            throw new InvalidOperationException("Não é permitido repetir rodadas no calendário.");
        }

        var rounds = await _db.Rounds
            .Where(r => roundIds.Contains(r.RoundId))
            .Select(r => new { r.RoundId, r.CompetitionId, r.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (rounds.Count != roundIds.Count)
        {
            throw new KeyNotFoundException("Uma ou mais rodadas não foram encontradas.");
        }

        var seasonCompetitionIds = await _db.Competitions
            .Where(c => c.SeasonId == command.SeasonId)
            .Select(c => c.CompetitionId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var seasonCompetitionSet = seasonCompetitionIds.ToHashSet();
        if (rounds.Any(r => !seasonCompetitionSet.Contains(r.CompetitionId)))
        {
            throw new InvalidOperationException("Todas as rodadas precisam pertencer a competições da temporada selecionada.");
        }

        var existingItems = await _db.SeasonSchedule
            .Where(s => s.SeasonId == command.SeasonId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (existingItems.Count > 0)
        {
            _db.SeasonSchedule.RemoveRange(existingItems);
        }

        foreach (var item in command.Items.OrderBy(i => i.Order))
        {
            _db.SeasonSchedule.Add(new SeasonScheduleItem
            {
                SeasonScheduleItemId = Guid.NewGuid(),
                SeasonId = command.SeasonId,
                RoundId = item.RoundId,
                Order = item.Order
            });
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return await GetScheduleAsync(command.SeasonId, ct).ConfigureAwait(false);
    }
}
