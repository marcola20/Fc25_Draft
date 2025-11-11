using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.DTOs.Seasons;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

public sealed class RoundSelectionService : IRoundSelectionService
{
    private readonly DraftDbContext _db;
    private readonly ILogger<RoundSelectionService> _logger;

    public RoundSelectionService(DraftDbContext db, ILogger<RoundSelectionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RoundSelectionDto?> GetByRoundAsync(Guid roundId, CancellationToken ct)
    {
        var selection = await _db.RoundSelections
            .AsNoTracking()
            .Where(s => s.RoundId == roundId)
            .Select(s => new RoundSelectionDto(
                s.RoundSelectionId,
                s.RoundId,
                s.CreatedAtUtc,
                s.Players
                    .OrderBy(p => p.Player.PositionId > 0 ? p.Player.PositionId : 999)
                    .ThenBy(p => p.Player.Name)
                    .Select(p => new RoundSelectionPlayerDto(
                        p.Player.PlayerGuid,
                        p.PlayerId,
                        p.Player.Name,
                        p.Player.Position.Name,
                        p.Player.PositionId > 0 ? p.Player.PositionId : 999))
                    .ToList()))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return selection;
    }

    public async Task<RoundSelectionDto> CreateOrGetAsync(Guid roundId, CancellationToken ct)
    {
        var existing = await GetByRoundAsync(roundId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var roundExists = await _db.Rounds
            .AsNoTracking()
            .AnyAsync(r => r.RoundId == roundId, ct)
            .ConfigureAwait(false);

        if (!roundExists)
        {
            throw new KeyNotFoundException("Rodada não encontrada.");
        }

        var selection = new RoundSelection
        {
            RoundSelectionId = Guid.NewGuid(),
            RoundId = roundId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.RoundSelections.Add(selection);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new RoundSelectionDto(selection.RoundSelectionId, selection.RoundId, selection.CreatedAtUtc, Array.Empty<RoundSelectionPlayerDto>());
    }

    public async Task<Result> AddPlayersAsync(Guid roundId, IEnumerable<Guid> playerIds, CancellationToken ct)
    {
        var normalized = playerIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        if (normalized.Length == 0)
        {
            return Result.Fail("Nenhum jogador informado.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var selection = await EnsureSelectionEntityAsync(roundId, ct).ConfigureAwait(false);

            var existingPlayerIds = await _db.RoundSelectionPlayers
                .AsNoTracking()
                .Where(p => p.RoundSelectionId == selection.RoundSelectionId)
                .Select(p => p.PlayerId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var existingSet = existingPlayerIds.ToHashSet();

            var players = await _db.Players
                .AsNoTracking()
                .Where(p => normalized.Contains(p.PlayerGuid))
                .Select(p => new { p.PlayerId, p.PlayerGuid })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (players.Count == 0)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                return Result.Fail("Jogadores não encontrados.");
            }

            var newEntries = new List<RoundSelectionPlayer>();

            foreach (var player in players)
            {
                if (existingSet.Contains(player.PlayerId))
                {
                    continue;
                }

                newEntries.Add(new RoundSelectionPlayer
                {
                    RoundSelectionId = selection.RoundSelectionId,
                    PlayerId = player.PlayerId,
                    AddedAtUtc = DateTime.UtcNow
                });
            }

            if (newEntries.Count == 0)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                return Result.Ok("Seleção atualizada com sucesso.");
            }

            if (existingSet.Count + newEntries.Count > 11)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                return Result.Fail("Você atingiu o limite de 11 jogadores.");
            }

            await _db.RoundSelectionPlayers.AddRangeAsync(newEntries, ct).ConfigureAwait(false);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            return Result.Ok("Seleção atualizada com sucesso.");
        }
        catch (KeyNotFoundException ex)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogError(ex, "Rodada não encontrada ao adicionar jogadores à seleção {RoundId}.", roundId);
            return Result.Fail("Rodada não encontrada.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogError(ex, "Erro ao adicionar jogadores à seleção da rodada {RoundId}.", roundId);
            return Result.Fail("Não foi possível atualizar a seleção da rodada.");
        }
    }

    public async Task<Result> RemovePlayerAsync(Guid roundId, Guid playerId, CancellationToken ct)
    {
        if (playerId == Guid.Empty)
        {
            return Result.Fail("Jogador inválido.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var selectionId = await _db.RoundSelections
                .AsNoTracking()
                .Where(s => s.RoundId == roundId)
                .Select(s => s.RoundSelectionId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (selectionId == Guid.Empty)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                return Result.Ok("Seleção atualizada com sucesso.");
            }

            var playerInternalId = await _db.Players
                .AsNoTracking()
                .Where(p => p.PlayerGuid == playerId)
                .Select(p => p.PlayerId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (playerInternalId == 0)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                return Result.Ok("Seleção atualizada com sucesso.");
            }

            var entry = await _db.RoundSelectionPlayers
                .FirstOrDefaultAsync(p =>
                    p.RoundSelectionId == selectionId &&
                    p.PlayerId == playerInternalId, ct)
                .ConfigureAwait(false);

            if (entry is null)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                return Result.Ok("Seleção atualizada com sucesso.");
            }

            _db.RoundSelectionPlayers.Remove(entry);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            return Result.Ok("Seleção atualizada com sucesso.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogError(ex, "Erro ao remover jogador da seleção da rodada {RoundId}.", roundId);
            return Result.Fail("Não foi possível atualizar a seleção da rodada.");
        }
    }

    private async Task<RoundSelection> EnsureSelectionEntityAsync(Guid roundId, CancellationToken ct)
    {
        var selection = await _db.RoundSelections
            .FirstOrDefaultAsync(s => s.RoundId == roundId, ct)
            .ConfigureAwait(false);

        if (selection is not null)
        {
            return selection;
        }

        var roundExists = await _db.Rounds
            .AsNoTracking()
            .AnyAsync(r => r.RoundId == roundId, ct)
            .ConfigureAwait(false);

        if (!roundExists)
        {
            throw new KeyNotFoundException("Rodada não encontrada.");
        }

        selection = new RoundSelection
        {
            RoundSelectionId = Guid.NewGuid(),
            RoundId = roundId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.RoundSelections.Add(selection);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return selection;
    }
}
