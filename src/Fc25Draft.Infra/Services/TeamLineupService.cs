using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

public class TeamLineupService : ITeamLineupService
{
    private readonly DraftDbContext _dbContext;
    private readonly ILogger<TeamLineupService> _logger;
    private readonly TimeProvider _timeProvider;

    public TeamLineupService(
        DraftDbContext dbContext,
        ILogger<TeamLineupService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<TeamLineupDto>> GetLineupsAsync(Guid teamId, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        await EnsureTeamExistsAsync(teamId, ct);

        var entities = await _dbContext.TeamLineups
            .AsNoTracking()
            .Where(l => l.TeamId == teamId)
            .Include(l => l.Slots)
                .ThenInclude(s => s.Player)
                    .ThenInclude(p => p!.Position)
            .OrderByDescending(l => l.IsActive)
            .ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<TeamLineupDto> CreateLineupAsync(Guid teamId, TeamLineupSaveRequestDto request, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await EnsureTeamExistsAsync(teamId, ct);

        var name = NormalizeName(request.Name);
        var template = LineupTemplateCatalog.GetTemplateOrDefault(request.Formation);
        var assignments = BuildAssignments(template, request);
        await ValidatePlayersAsync(teamId, template, assignments, ct);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var currentCount = await _dbContext.TeamLineups.CountAsync(l => l.TeamId == teamId, ct);
                if (currentCount >= 3)
                {
                    throw new InvalidOperationException("O time já possui 3 escalações cadastradas.");
                }

                var lineup = new TeamLineup
                {
                    LineupId = Guid.NewGuid(),
                    TeamId = teamId,
                    Name = name,
                    Formation = template.Formation,
                    IsActive = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                foreach (var slotTemplate in template.AllSlots)
                {
                    assignments.TryGetValue(slotTemplate.SlotCode, out var playerId);

                    lineup.Slots.Add(new TeamLineupSlot
                    {
                        LineupSlotId = Guid.NewGuid(),
                        LineupId = lineup.LineupId,
                        SlotCode = slotTemplate.SlotCode,
                        DisplayName = slotTemplate.DisplayName,
                        IsBench = slotTemplate.IsBench,
                        Order = slotTemplate.Order,
                        PlayerId = playerId
                    });
                }

                await _dbContext.TeamLineups.AddAsync(lineup, ct);

                var existingActives = await _dbContext.TeamLineups
                    .Where(l => l.TeamId == teamId && l.IsActive)
                    .ToListAsync(ct);

                var shouldActivate = request.IsActive || existingActives.Count == 0;
                if (shouldActivate)
                {
                    lineup.IsActive = true;
                    foreach (var other in existingActives)
                    {
                        if (other.IsActive)
                        {
                            other.IsActive = false;
                            other.UpdatedAt = now;
                        }
                    }
                }

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return await LoadLineupDtoAsync(lineup.LineupId, ct);
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            catch (KeyNotFoundException)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Erro ao criar escalação para o time {TeamId}.", teamId);
                throw new InvalidOperationException("Não foi possível salvar a escalação.", ex);
            }
        });
    }

    public async Task<TeamLineupDto> UpdateLineupAsync(Guid teamId, Guid lineupId, TeamLineupSaveRequestDto request, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (lineupId == Guid.Empty)
        {
            throw new ArgumentException("Escalação inválida.", nameof(lineupId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await EnsureTeamExistsAsync(teamId, ct);

        var template = LineupTemplateCatalog.GetTemplateOrDefault(request.Formation);
        var assignments = BuildAssignments(template, request);
        await ValidatePlayersAsync(teamId, template, assignments, ct);
        var name = NormalizeName(request.Name);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var lineup = await _dbContext.TeamLineups
                    .Include(l => l.Slots)
                    .FirstOrDefaultAsync(l => l.LineupId == lineupId && l.TeamId == teamId, ct)
                    ?? throw new KeyNotFoundException("Escalação não encontrada.");

                lineup.Name = name;
                lineup.Formation = template.Formation;
                lineup.UpdatedAt = now;

                ApplyTemplateSlots(lineup, template, assignments);

                var otherLineups = await _dbContext.TeamLineups
                    .Where(l => l.TeamId == teamId && l.LineupId != lineupId)
                    .ToListAsync(ct);

                var shouldActivate = request.IsActive;
                if (!shouldActivate && !otherLineups.Any(l => l.IsActive))
                {
                    shouldActivate = true;
                }

                if (shouldActivate)
                {
                    lineup.IsActive = true;
                    foreach (var other in otherLineups)
                    {
                        if (other.IsActive)
                        {
                            other.IsActive = false;
                            other.UpdatedAt = now;
                        }
                    }
                }
                else if (lineup.IsActive && !request.IsActive)
                {
                    var fallback = otherLineups
                        .OrderByDescending(l => l.IsActive)
                        .ThenByDescending(l => l.UpdatedAt)
                        .FirstOrDefault();

                    if (fallback is null)
                    {
                        lineup.IsActive = true;
                    }
                    else
                    {
                        lineup.IsActive = false;
                        if (!fallback.IsActive)
                        {
                            fallback.IsActive = true;
                            fallback.UpdatedAt = now;
                        }
                    }
                }

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return await LoadLineupDtoAsync(lineup.LineupId, ct);
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            catch (KeyNotFoundException)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Erro ao atualizar a escalação {LineupId} do time {TeamId}.", lineupId, teamId);
                throw new InvalidOperationException("Não foi possível salvar a escalação.", ex);
            }
        });
    }

    public async Task DeleteLineupAsync(Guid teamId, Guid lineupId, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (lineupId == Guid.Empty)
        {
            throw new ArgumentException("Escalação inválida.", nameof(lineupId));
        }

        await EnsureTeamExistsAsync(teamId, ct);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var lineup = await _dbContext.TeamLineups
                    .Include(l => l.Slots)
                    .FirstOrDefaultAsync(l => l.LineupId == lineupId && l.TeamId == teamId, ct)
                    ?? throw new KeyNotFoundException("Escalação não encontrada.");

                var wasActive = lineup.IsActive;
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                _dbContext.TeamLineups.Remove(lineup);

                if (wasActive)
                {
                    var fallback = await _dbContext.TeamLineups
                        .Where(l => l.TeamId == teamId && l.LineupId != lineupId)
                        .OrderByDescending(l => l.IsActive)
                        .ThenByDescending(l => l.UpdatedAt)
                        .FirstOrDefaultAsync(ct);

                    if (fallback is not null && !fallback.IsActive)
                    {
                        fallback.IsActive = true;
                        fallback.UpdatedAt = now;
                    }
                }

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            catch (KeyNotFoundException)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Erro ao excluir a escalação {LineupId} do time {TeamId}.", lineupId, teamId);
                throw new InvalidOperationException("Não foi possível excluir a escalação.", ex);
            }
        });
    }

    public async Task SetActiveLineupAsync(Guid teamId, Guid lineupId, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (lineupId == Guid.Empty)
        {
            throw new ArgumentException("Escalação inválida.", nameof(lineupId));
        }

        await EnsureTeamExistsAsync(teamId, ct);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var lineups = await _dbContext.TeamLineups
                    .Where(l => l.TeamId == teamId)
                    .ToListAsync(ct);

                var target = lineups.FirstOrDefault(l => l.LineupId == lineupId)
                    ?? throw new KeyNotFoundException("Escalação não encontrada.");

                var now = _timeProvider.GetUtcNow().UtcDateTime;

                foreach (var lineup in lineups)
                {
                    var shouldBeActive = lineup.LineupId == lineupId;
                    if (lineup.IsActive != shouldBeActive)
                    {
                        lineup.IsActive = shouldBeActive;
                        lineup.UpdatedAt = now;
                    }
                }

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            catch (KeyNotFoundException)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Erro ao definir a escalação ativa {LineupId} do time {TeamId}.", lineupId, teamId);
                throw new InvalidOperationException("Não foi possível definir a escalação ativa.", ex);
            }
        });
    }

    private async Task EnsureTeamExistsAsync(Guid teamId, CancellationToken ct)
    {
        var exists = await _dbContext.Teams.AnyAsync(t => t.TeamId == teamId, ct);
        if (!exists)
        {
            throw new KeyNotFoundException("Time não encontrado.");
        }
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("O nome da escalação é obrigatório.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length is < 3 or > 80)
        {
            throw new InvalidOperationException("O nome da escalação deve ter entre 3 e 80 caracteres.");
        }

        return trimmed;
    }

    private static Dictionary<string, int?> BuildAssignments(LineupTemplate template, TeamLineupSaveRequestDto request)
    {
        var assignments = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

        void Apply(IEnumerable<TeamLineupSlotAssignmentDto> slots)
        {
            if (slots is null)
            {
                return;
            }

            foreach (var assignment in slots)
            {
                if (assignment is null || string.IsNullOrWhiteSpace(assignment.SlotCode))
                {
                    continue;
                }

                template.GetSlot(assignment.SlotCode); // valida existência
                assignments[assignment.SlotCode.Trim()] = assignment.PlayerId;
            }
        }

        Apply(request.Starters);
        Apply(request.Bench);

        foreach (var slot in template.AllSlots)
        {
            if (!assignments.ContainsKey(slot.SlotCode))
            {
                assignments[slot.SlotCode] = null;
            }
        }

        var selectedPlayerIds = assignments.Values
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (selectedPlayerIds.Count != selectedPlayerIds.Distinct().Count())
        {
            throw new InvalidOperationException("Não é permitido repetir um jogador na mesma escalação.");
        }

        return assignments;
    }

    private async Task ValidatePlayersAsync(
        Guid teamId,
        LineupTemplate template,
        IReadOnlyDictionary<string, int?> assignments,
        CancellationToken ct)
    {
        var selectedPlayerIds = assignments.Values
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        if (selectedPlayerIds.Count == 0)
        {
            return;
        }

        var rosterPlayers = await _dbContext.TeamRosters
            .AsNoTracking()
            .Where(r => r.TeamId == teamId && selectedPlayerIds.Contains(r.PlayerId))
            .Select(r => new { r.PlayerId, r.Player.PositionId })
            .ToListAsync(ct);

        if (rosterPlayers.Count != selectedPlayerIds.Count)
        {
            throw new InvalidOperationException("Alguns jogadores informados não pertencem ao elenco da equipe.");
        }

        var rosterPositions = rosterPlayers.ToDictionary(x => x.PlayerId, x => x.PositionId);

        foreach (var (slotCode, playerId) in assignments)
        {
            if (!playerId.HasValue)
            {
                continue;
            }

            var slot = template.GetSlot(slotCode);
            if (!slot.IsBench && slot.AllowedPositionIds.Count > 0)
            {
                var positionId = rosterPositions[playerId.Value];
                if (!slot.AllowedPositionIds.Contains(positionId))
                {
                    throw new InvalidOperationException($"O jogador selecionado não pode atuar na posição '{slot.DisplayName}'.");
                }
            }
        }

        return;
    }

    private void ApplyTemplateSlots(TeamLineup lineup, LineupTemplate template, IReadOnlyDictionary<string, int?> assignments)
    {
        var existingByCode = lineup.Slots.ToDictionary(s => s.SlotCode, StringComparer.OrdinalIgnoreCase);

        foreach (var slotTemplate in template.AllSlots)
        {
            assignments.TryGetValue(slotTemplate.SlotCode, out var playerId);

            if (existingByCode.TryGetValue(slotTemplate.SlotCode, out var slot))
            {
                slot.DisplayName = slotTemplate.DisplayName;
                slot.IsBench = slotTemplate.IsBench;
                slot.Order = slotTemplate.Order;
                slot.PlayerId = playerId;
            }
            else
            {
                lineup.Slots.Add(new TeamLineupSlot
                {
                    LineupSlotId = Guid.NewGuid(),
                    LineupId = lineup.LineupId,
                    SlotCode = slotTemplate.SlotCode,
                    DisplayName = slotTemplate.DisplayName,
                    IsBench = slotTemplate.IsBench,
                    Order = slotTemplate.Order,
                    PlayerId = playerId
                });
            }
        }

        var slotsToRemove = lineup.Slots
            .Where(s => !template.AllSlots.Any(t => t.SlotCode.Equals(s.SlotCode, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var slot in slotsToRemove)
        {
            lineup.Slots.Remove(slot);
            _dbContext.TeamLineupSlots.Remove(slot);
        }
    }

    private async Task<TeamLineupDto> LoadLineupDtoAsync(Guid lineupId, CancellationToken ct)
    {
        var entity = await _dbContext.TeamLineups
            .AsNoTracking()
            .Include(l => l.Slots)
                .ThenInclude(s => s.Player)
                    .ThenInclude(p => p!.Position)
            .FirstOrDefaultAsync(l => l.LineupId == lineupId, ct)
            ?? throw new KeyNotFoundException("Escalação não encontrada.");

        return MapToDto(entity);
    }

    private static TeamLineupDto MapToDto(TeamLineup entity)
    {
        var template = LineupTemplateCatalog.GetTemplateOrDefault(entity.Formation);
        var orderedSlots = entity.Slots
            .Select(slot =>
            {
                var slotTemplate = template.GetSlot(slot.SlotCode);
                var allowed = slotTemplate.AllowedPositionIds.Count > 0
                    ? slotTemplate.AllowedPositionIds.ToArray()
                    : Array.Empty<short>();

                TeamLineupSlotPlayerDto? player = null;
                if (slot.Player is not null)
                {
                    player = new TeamLineupSlotPlayerDto(
                        slot.Player.PlayerId,
                        slot.Player.PlayerGuid,
                        slot.Player.Name,
                        slot.Player.Position.Name,
                        slot.Player.PositionId);
                }

                return new TeamLineupSlotDto(
                    slot.LineupSlotId,
                    slotTemplate.SlotCode,
                    slotTemplate.DisplayName,
                    slotTemplate.IsBench,
                    slotTemplate.Order,
                    allowed,
                    player);
            })
            .OrderBy(s => s.IsBench)
            .ThenBy(s => s.Order)
            .ToList();

        var starters = orderedSlots.Where(s => !s.IsBench).ToList();
        var bench = orderedSlots.Where(s => s.IsBench).ToList();

        return new TeamLineupDto(
            entity.LineupId,
            entity.TeamId,
            entity.Name,
            template.Formation,
            entity.IsActive,
            starters,
            bench,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
