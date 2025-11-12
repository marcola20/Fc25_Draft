using System.Collections.Generic;
using System.Linq;
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
    private readonly TimeProvider _timeProvider;

    public RoundSelectionService(
        DraftDbContext db,
        ILogger<RoundSelectionService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RoundSelectionDto?> GetByRoundAsync(Guid roundId, CancellationToken ct)
    {
        if (roundId == Guid.Empty)
        {
            return null;
        }

        try
        {
            var selection = await QuerySelectionAsync(roundId, ct).ConfigureAwait(false);
            if (selection is not null)
            {
                return selection;
            }

            var exists = await _db.Rounds
                .AsNoTracking()
                .AnyAsync(r => r.RoundId == roundId, ct)
                .ConfigureAwait(false);

            if (!exists)
            {
                throw new KeyNotFoundException("Rodada não encontrada.");
            }

            return new RoundSelectionDto(roundId, Array.Empty<RoundSelectionPlayerDto>());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar a seleção da rodada {RoundId}.", roundId);
            throw new InvalidOperationException("Não foi possível carregar a seleção da rodada.");
        }
    }

    public async Task<RoundSelectionDto> CreateOrGetAsync(Guid roundId, CancellationToken ct)
    {
        if (roundId == Guid.Empty)
        {
            throw new ArgumentException("Rodada inválida.", nameof(roundId));
        }

        try
        {
            var selection = await QuerySelectionAsync(roundId, ct).ConfigureAwait(false);
            if (selection is not null)
            {
                return selection;
            }

            var exists = await _db.Rounds
                .AsNoTracking()
                .AnyAsync(r => r.RoundId == roundId, ct)
                .ConfigureAwait(false);

            if (!exists)
            {
                throw new KeyNotFoundException("Rodada não encontrada.");
            }

            var entity = new RoundSelection
            {
                RoundSelectionId = Guid.NewGuid(),
                RoundId = roundId,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            _db.RoundSelections.Add(entity);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            return new RoundSelectionDto(roundId, Array.Empty<RoundSelectionPlayerDto>());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar a seleção da rodada {RoundId}.", roundId);
            throw new InvalidOperationException("Não foi possível criar a seleção da rodada.");
        }
    }

    public async Task<OperationResultDto> AddPlayersAsync(Guid roundId, IEnumerable<Guid> playerIds, CancellationToken ct)
    {
        if (roundId == Guid.Empty)
        {
            return new OperationResultDto(false, "Rodada inválida.");
        }

        var ids = playerIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0)
        {
            return new OperationResultDto(true, "Nenhum jogador selecionado.");
        }

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            var result = new OperationResultDto(true, "Seleção atualizada com sucesso.");

            await strategy.ExecuteAsync(async innerCt =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(innerCt).ConfigureAwait(false);

                var selection = await _db.RoundSelections
                    .Include(s => s.Players)
                    .FirstOrDefaultAsync(s => s.RoundId == roundId, innerCt)
                    .ConfigureAwait(false);

                var existingCount = selection?.Players.Count ?? 0;
                var existingPlayers = selection?.Players
                        .Select(p => p.PlayerGuid)
                        .ToHashSet() ?? new HashSet<Guid>();

                var candidates = ids.Where(id => !existingPlayers.Contains(id)).ToList();
                if (candidates.Count == 0)
                {
                    result = new OperationResultDto(true, "Jogadores já fazem parte da seleção.");
                    return;
                }

                var validPlayers = await _db.Players
                    .AsNoTracking()
                    .Where(p => candidates.Contains(p.PlayerGuid))
                    .Select(p => p.PlayerGuid)
                    .ToListAsync(innerCt)
                    .ConfigureAwait(false);

                if (validPlayers.Count == 0)
                {
                    result = new OperationResultDto(false, "Nenhum jogador válido encontrado.");
                    return;
                }

                if (existingCount + validPlayers.Count > 11)
                {
                    result = new OperationResultDto(false, "Você atingiu o limite de 11 jogadores.");
                    return;
                }

                if (selection is null)
                {
                    var roundExists = await _db.Rounds
                        .AsNoTracking()
                        .AnyAsync(r => r.RoundId == roundId, innerCt)
                        .ConfigureAwait(false);

                    if (!roundExists)
                    {
                        result = new OperationResultDto(false, "Rodada não encontrada.");
                        return;
                    }

                    selection = new RoundSelection
                    {
                        RoundSelectionId = Guid.NewGuid(),
                        RoundId = roundId,
                        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                    };

                    _db.RoundSelections.Add(selection);
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                foreach (var playerGuid in validPlayers)
                {
                    selection.Players.Add(new RoundSelectionPlayer
                    {
                        RoundSelectionId = selection.RoundSelectionId,
                        PlayerGuid = playerGuid,
                        AddedAt = now
                    });
                }

                await _db.SaveChangesAsync(innerCt).ConfigureAwait(false);
                await transaction.CommitAsync(innerCt).ConfigureAwait(false);
                result = new OperationResultDto(true, "Seleção atualizada com sucesso.");
            }, ct).ConfigureAwait(false);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar jogadores à seleção da rodada {RoundId}.", roundId);
            return new OperationResultDto(false, "Não foi possível atualizar a seleção da rodada.");
        }
    }

    public async Task<OperationResultDto> RemovePlayerAsync(Guid roundId, Guid playerId, CancellationToken ct)
    {
        if (roundId == Guid.Empty || playerId == Guid.Empty)
        {
            return new OperationResultDto(false, "Parâmetros inválidos.");
        }

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            var result = new OperationResultDto(true, "Seleção atualizada com sucesso.");

            await strategy.ExecuteAsync(async innerCt =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(innerCt).ConfigureAwait(false);

                var selection = await _db.RoundSelections
                    .Include(s => s.Players)
                    .FirstOrDefaultAsync(s => s.RoundId == roundId, innerCt)
                    .ConfigureAwait(false);

                if (selection is null)
                {
                    var exists = await _db.Rounds
                        .AsNoTracking()
                        .AnyAsync(r => r.RoundId == roundId, innerCt)
                        .ConfigureAwait(false);

                    if (!exists)
                    {
                        result = new OperationResultDto(false, "Rodada não encontrada.");
                    }

                    return;
                }

                var entry = selection.Players.FirstOrDefault(p => p.PlayerGuid == playerId);
                if (entry is null)
                {
                    return;
                }

                _db.RoundSelectionPlayers.Remove(entry);
                await _db.SaveChangesAsync(innerCt).ConfigureAwait(false);
                await transaction.CommitAsync(innerCt).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover o jogador {PlayerId} da seleção da rodada {RoundId}.", playerId, roundId);
            return new OperationResultDto(false, "Não foi possível atualizar a seleção da rodada.");
        }
    }

    private async Task<RoundSelectionDto?> QuerySelectionAsync(Guid roundId, CancellationToken ct)
    {
        return await _db.RoundSelections
            .AsNoTracking()
            .Where(s => s.RoundId == roundId)
            .Select(s => new RoundSelectionDto(
                s.RoundId,
                s.Players
                    .OrderBy(p => (int)(p.Player.PositionId == 0 ? 999 : p.Player.PositionId))
                    .ThenBy(p => p.Player.Name)
                    .Select(p => new RoundSelectionPlayerDto(
                        p.Player.PlayerGuid,
                        p.Player.Name,
                        p.Player.Position.Name,
                        p.Player.PositionId == 0 ? 999 : p.Player.PositionId,
                        p.Player.TeamRosters
                            .Select(r => r.Team.TeamName)
                            .FirstOrDefault()))
                    .ToList()))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
