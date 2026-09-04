using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class DraftWishlistService : IDraftWishlistService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public DraftWishlistService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<DraftWishlistDto> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        var team = await ResolveTeamAsync(token, ct);
        return await BuildDtoAsync(team.TeamId, team.TeamName, ct);
    }

    public async Task<DraftWishlistDto> SaveAsync(string token, IReadOnlyList<int> playerIds, CancellationToken ct = default)
    {
        var team = await ResolveTeamAsync(token, ct);

        if (playerIds is null || playerIds.Count == 0)
            throw new InvalidOperationException("Informe os jogadores da lista.");

        var ids = playerIds.Distinct().ToList();
        if (ids.Count != playerIds.Count)
            throw new InvalidOperationException("A lista contém jogadores repetidos.");

        if (ids.Count != DraftWishlistRules.MaxJogadores)
            throw new InvalidOperationException($"A lista deve conter exatamente {DraftWishlistRules.MaxJogadores} jogadores.");

        var players = await _db.Players
            .AsNoTracking()
            .Where(p => ids.Contains(p.PlayerId))
            .Select(p => new { p.PlayerId, p.Name, p.Overall, Escolhido = p.TeamRosters.Any() })
            .ToListAsync(ct);

        if (players.Count != ids.Count)
            throw new InvalidOperationException("Um ou mais jogadores não foram encontrados.");

        var escolhidos = players.Where(p => p.Escolhido).Select(p => p.Name).ToList();
        if (escolhidos.Count > 0)
            throw new InvalidOperationException($"Jogadores já pertencem a um time: {string.Join(", ", escolhidos)}.");

        var foraDaFaixa = players
            .Where(p => p.Overall < DraftWishlistRules.OverallMinimo || p.Overall > DraftWishlistRules.OverallMaximo)
            .Select(p => $"{p.Name} ({p.Overall})")
            .ToList();
        if (foraDaFaixa.Count > 0)
            throw new InvalidOperationException(
                $"Jogadores fora da faixa de overall do draft ({DraftWishlistRules.OverallMinimo} a {DraftWishlistRules.OverallMaximo}): {string.Join(", ", foraDaFaixa)}.");

        var now = _time.GetUtcNow().UtcDateTime;

        var existentes = await _db.DraftWishlistEntries
            .Where(e => e.TeamId == team.TeamId)
            .ToListAsync(ct);
        _db.DraftWishlistEntries.RemoveRange(existentes);

        for (var i = 0; i < ids.Count; i++)
        {
            _db.DraftWishlistEntries.Add(new DraftWishlistEntry
            {
                DraftWishlistEntryId = Guid.NewGuid(),
                TeamId = team.TeamId,
                PlayerId = ids[i],
                Ordem = i + 1,
                CriadoEm = now
            });
        }

        await _db.SaveChangesAsync(ct);

        return await BuildDtoAsync(team.TeamId, team.TeamName, ct);
    }

    public async Task<IReadOnlyList<DraftWishlistDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.DraftWishlistEntries
            .AsNoTracking()
            .Select(e => new
            {
                e.TeamId,
                e.Team.TeamName,
                e.CriadoEm,
                Jogador = new DraftWishlistPlayerDto(
                    e.Ordem,
                    e.PlayerId,
                    e.Player.Name,
                    e.Player.PositionId,
                    e.Player.Position.Name,
                    e.Player.Overall,
                    e.Player.Age,
                    !e.Player.TeamRosters.Any())
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.TeamId, r.TeamName })
            .OrderBy(g => g.Key.TeamName)
            .Select(g => new DraftWishlistDto(
                g.Key.TeamId,
                g.Key.TeamName,
                g.Max(r => (DateTime?)r.CriadoEm),
                g.Select(r => r.Jogador).OrderBy(j => j.Ordem).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<DraftWishlistVoteDto>> GetVotesAsync(CancellationToken ct = default)
    {
        var rows = await _db.DraftWishlistEntries
            .AsNoTracking()
            .Select(e => new
            {
                e.PlayerId,
                e.Player.Name,
                e.Player.PositionId,
                PositionName = e.Player.Position.Name,
                e.Player.Overall,
                e.Player.Age,
                Disponivel = !e.Player.TeamRosters.Any(),
                e.TeamId,
                e.Team.TeamName,
                e.Ordem
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.PlayerId)
            .Select(g =>
            {
                var first = g.First();
                var times = g
                    .OrderBy(r => r.Ordem)
                    .ThenBy(r => r.TeamName)
                    .Select(r => new DraftWishlistVoteTeamDto(r.TeamId, r.TeamName, r.Ordem))
                    .ToList();

                return new DraftWishlistVoteDto(
                    first.PlayerId,
                    first.Name,
                    first.PositionId,
                    first.PositionName,
                    first.Overall,
                    first.Age,
                    first.Disponivel,
                    times.Count,
                    times.Min(t => t.Ordem),
                    times);
            })
            .OrderByDescending(v => v.Votos)
            .ThenBy(v => v.MelhorPosicao)
            .ThenByDescending(v => v.Overall)
            .ThenBy(v => v.Name)
            .ToList();
    }

    private async Task<Team> ResolveTeamAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("Token do time obrigatório.");

        var normalized = token.Trim();
        var team = await _db.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == normalized || t.AuxToken == normalized, ct);

        return team ?? throw new UnauthorizedAccessException("Token do time inválido.");
    }

    private async Task<DraftWishlistDto> BuildDtoAsync(Guid teamId, string teamName, CancellationToken ct)
    {
        var rows = await _db.DraftWishlistEntries
            .AsNoTracking()
            .Where(e => e.TeamId == teamId)
            .OrderBy(e => e.Ordem)
            .Select(e => new
            {
                e.CriadoEm,
                Jogador = new DraftWishlistPlayerDto(
                    e.Ordem,
                    e.PlayerId,
                    e.Player.Name,
                    e.Player.PositionId,
                    e.Player.Position.Name,
                    e.Player.Overall,
                    e.Player.Age,
                    !e.Player.TeamRosters.Any())
            })
            .ToListAsync(ct);

        return new DraftWishlistDto(
            teamId,
            teamName,
            rows.Count == 0 ? null : rows.Max(r => r.CriadoEm),
            rows.Select(r => r.Jogador).ToList());
    }
}
