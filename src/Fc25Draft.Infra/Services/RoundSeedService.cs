using System.Collections.Generic;
using System.Linq;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public sealed class RoundSeedService : IRoundSeedService
{
    private readonly DraftDbContext _db;

    public RoundSeedService(DraftDbContext db) => _db = db;

    public async Task EnsureDefaultSeasonAsync(CancellationToken ct)
    {
        if (await _db.Seasons.AnyAsync(ct).ConfigureAwait(false)) return;

        var season = new Season { SeasonId = Guid.NewGuid(), Name = "Temporada 1" };
        _db.Seasons.Add(season);

        var brasileirao = new Competition
        {
            CompetitionId = Guid.NewGuid(),
            SeasonId = season.SeasonId,
            Name = "Brasileirão CBFV",
            Order = 1
        };
        var copa = new Competition
        {
            CompetitionId = Guid.NewGuid(),
            SeasonId = season.SeasonId,
            Name = "Copa CBFV",
            Order = 2
        };
        _db.Competitions.AddRange(brasileirao, copa);

        var roundsLiga = new[]
        {
            "Rodada 1","Rodada 2","Rodada 3","Rodada 4","Rodada 5","Rodada 6",
            "Rodada 7","Rodada 8","Rodada 9","Rodada 10","Rodada 11","Rodada 12",
            "Rodada 13","Quartas","Semi","Final"
        };
        var roundLigaEntities = roundsLiga.Select(name => new Round
        {
            RoundId = Guid.NewGuid(),
            CompetitionId = brasileirao.CompetitionId,
            Name = name
        }).ToList();

        var roundsCopa = new[]
        {
            "Rodada 1","Rodada 2","Rodada 3","Rodada 4","Rodada 5","Rodada 6",
            "Quartas","Semi","Final"
        };
        var roundCopaEntities = roundsCopa.Select(name => new Round
        {
            RoundId = Guid.NewGuid(),
            CompetitionId = copa.CompetitionId,
            Name = name
        }).ToList();

        _db.Rounds.AddRange(roundLigaEntities);
        _db.Rounds.AddRange(roundCopaEntities);

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        var ordered = new List<Guid>
        {
            roundLigaEntities.First(r=>r.Name=="Rodada 1").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 2").RoundId,
            roundCopaEntities.First(r=>r.Name=="Rodada 1").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 3").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 4").RoundId,
            roundCopaEntities.First(r=>r.Name=="Rodada 2").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 5").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 6").RoundId,
            roundCopaEntities.First(r=>r.Name=="Rodada 3").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 7").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 8").RoundId,
            roundCopaEntities.First(r=>r.Name=="Rodada 4").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 9").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 10").RoundId,
            roundCopaEntities.First(r=>r.Name=="Rodada 5").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 11").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 12").RoundId,
            roundCopaEntities.First(r=>r.Name=="Rodada 6").RoundId,
            roundLigaEntities.First(r=>r.Name=="Rodada 13").RoundId,
            roundLigaEntities.First(r=>r.Name=="Quartas").RoundId,
            roundCopaEntities.First(r=>r.Name=="Quartas").RoundId,
            roundLigaEntities.First(r=>r.Name=="Semi").RoundId,
            roundCopaEntities.First(r=>r.Name=="Semi").RoundId,
            roundLigaEntities.First(r=>r.Name=="Final").RoundId,
            roundCopaEntities.First(r=>r.Name=="Final").RoundId,
        };

        int order = 1;
        foreach (var rid in ordered)
        {
            _db.SeasonSchedule.Add(new SeasonScheduleItem
            {
                SeasonScheduleItemId = Guid.NewGuid(),
                SeasonId = season.SeasonId,
                RoundId = rid,
                Order = order++
            });
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
