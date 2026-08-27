using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
            .Include(l => l.CaptainPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.ShortFreeKick1Player).ThenInclude(p => p!.Position)
            .Include(l => l.ShortFreeKick2Player).ThenInclude(p => p!.Position)
            .Include(l => l.LongFreeKickPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.PenaltiesPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.CornerLeftPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.CornerRightPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.AttackingPlayer1).ThenInclude(p => p!.Position)
            .Include(l => l.AttackingPlayer2).ThenInclude(p => p!.Position)
            .Include(l => l.AttackingPlayer3).ThenInclude(p => p!.Position)
            .Include(l => l.OffensiveInstructions)
            .Include(l => l.DefensiveInstructions)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.AttackPlayer1).ThenInclude(p => p!.Position)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.AttackPlayer2).ThenInclude(p => p!.Position)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.DefensePlayer1).ThenInclude(p => p!.Position)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.DefensePlayer2).ThenInclude(p => p!.Position)
            .OrderByDescending(l => l.IsActive)
            .ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AdminLineupOverviewDto>> GetAdminLineupsAsync(Guid? teamId, CancellationToken ct)
    {
        IQueryable<TeamLineup> query = _dbContext.TeamLineups
            .AsNoTracking()
            .Include(l => l.Team)
            .Include(l => l.Slots)
                .ThenInclude(s => s.Player)
                    .ThenInclude(p => p!.Position)
            .Include(l => l.CaptainPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.ShortFreeKick1Player).ThenInclude(p => p!.Position)
            .Include(l => l.ShortFreeKick2Player).ThenInclude(p => p!.Position)
            .Include(l => l.LongFreeKickPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.PenaltiesPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.CornerLeftPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.CornerRightPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.AttackingPlayer1).ThenInclude(p => p!.Position)
            .Include(l => l.AttackingPlayer2).ThenInclude(p => p!.Position)
            .Include(l => l.AttackingPlayer3).ThenInclude(p => p!.Position)
            .Include(l => l.OffensiveInstructions)
            .Include(l => l.DefensiveInstructions)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.AttackPlayer1).ThenInclude(p => p!.Position)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.AttackPlayer2).ThenInclude(p => p!.Position)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.DefensePlayer1).ThenInclude(p => p!.Position)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.DefensePlayer2).ThenInclude(p => p!.Position);

        if (teamId.HasValue && teamId.Value != Guid.Empty)
        {
            await EnsureTeamExistsAsync(teamId.Value, ct);
            query = query.Where(l => l.TeamId == teamId.Value);
        }
        else
        {
            query = query.Where(l => l.IsActive);
        }

        var entities = await query
            .OrderBy(l => l.Team.TeamName)
            .ThenByDescending(l => l.IsActive)
            .ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToAdminDto).ToList();
    }

    public async Task AcknowledgeLineupAsync(Guid lineupId, CancellationToken ct)
    {
        if (lineupId == Guid.Empty) throw new ArgumentException("Escalação inválida.", nameof(lineupId));

        var dto = await LoadLineupDtoAsync(lineupId, ct);

        var entity = await _dbContext.TeamLineups.FirstOrDefaultAsync(l => l.LineupId == lineupId, ct)
            ?? throw new KeyNotFoundException("Escalação não encontrada.");

        entity.LastSeenSnapshotJson = JsonSerializer.Serialize(dto);
        entity.LastSeenAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<TeamLineupDto> CreateLineupAsync(Guid teamId, TeamLineupSaveRequestDto request, CancellationToken ct)
    {
        if (teamId == Guid.Empty) throw new ArgumentException("Time inválido.", nameof(teamId));
        if (request is null) throw new ArgumentNullException(nameof(request));

        await EnsureTeamExistsAsync(teamId, ct);

        var name = NormalizeName(request.Name);
        var autoSubstitution = NormalizeAutoSubstitution(request.AutoSubstitution);
        var template = LineupTemplateCatalog.GetTemplateOrDefault(request.Formation);
        var assignments = BuildAssignments(template, request);
        await ValidatePlayersAsync(teamId, template, assignments, ct);
        var normalizedRoles = NormalizeRoles(template, assignments, request.Roles);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var newLineupId = await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var currentCount = await _dbContext.TeamLineups.CountAsync(l => l.TeamId == teamId, ct);
                if (currentCount >= 3)
                    throw new InvalidOperationException("O time já possui 3 escalações cadastradas.");

                var lineup = new TeamLineup
                {
                    LineupId = Guid.NewGuid(),
                    TeamId = teamId,
                    Name = name,
                    Formation = template.Formation,
                    AutoSubstitution = autoSubstitution,
                    IsActive = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                ApplyRoles(lineup, normalizedRoles);

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
                        if (other.IsActive) { other.IsActive = false; other.UpdatedAt = now; }
                    }
                }

                if (request.OffensiveInstructions is not null)
                {
                    var oi = BuildOffensiveInstructions(lineup.LineupId, request.OffensiveInstructions);
                    await _dbContext.TeamLineupOffensiveInstructions.AddAsync(oi, ct);
                }

                if (request.DefensiveInstructions is not null)
                {
                    var di = BuildDefensiveInstructions(lineup.LineupId, request.DefensiveInstructions);
                    await _dbContext.TeamLineupDefensiveInstructions.AddAsync(di, ct);
                }

                if (request.AdvancedInstructions is not null)
                {
                    var ai = BuildAdvancedInstructions(lineup.LineupId, request.AdvancedInstructions);
                    await _dbContext.TeamLineupAdvancedInstructions.AddAsync(ai, ct);
                }

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return lineup.LineupId;
            }
            catch (InvalidOperationException) { await transaction.RollbackAsync(ct); throw; }
            catch (KeyNotFoundException) { await transaction.RollbackAsync(ct); throw; }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Erro ao criar escalação para o time {TeamId}.", teamId);
                throw new InvalidOperationException("Não foi possível salvar a escalação.", ex);
            }
        });

        var created = await LoadLineupDtoAsync(newLineupId, ct);

        // Retrato inicial: até o ADM olhar de novo, nada aqui conta como "mudança".
        try
        {
            var entity = await _dbContext.TeamLineups.FirstOrDefaultAsync(l => l.LineupId == newLineupId, ct);
            if (entity is not null)
            {
                entity.LastSeenSnapshotJson = JsonSerializer.Serialize(created);
                entity.LastSeenAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                await _dbContext.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível registrar o retrato inicial da escalação {LineupId}.", newLineupId);
        }

        return created;
    }

    public async Task<TeamLineupDto> UpdateLineupAsync(Guid teamId, Guid lineupId, TeamLineupSaveRequestDto request, CancellationToken ct)
    {
        if (teamId == Guid.Empty) throw new ArgumentException("Time inválido.", nameof(teamId));
        if (lineupId == Guid.Empty) throw new ArgumentException("Escalação inválida.", nameof(lineupId));
        if (request is null) throw new ArgumentNullException(nameof(request));

        await EnsureTeamExistsAsync(teamId, ct);

        var template = LineupTemplateCatalog.GetTemplateOrDefault(request.Formation);
        var assignments = BuildAssignments(template, request);
        await ValidatePlayersAsync(teamId, template, assignments, ct);
        var name = NormalizeName(request.Name);
        var autoSubstitution = NormalizeAutoSubstitution(request.AutoSubstitution);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var lineup = await _dbContext.TeamLineups
                    .Include(l => l.Slots)
                    .FirstOrDefaultAsync(l => l.LineupId == lineupId && l.TeamId == teamId, ct)
                    ?? throw new KeyNotFoundException("Escalação não encontrada.");

                lineup.Name = name;
                lineup.Formation = template.Formation;
                lineup.AutoSubstitution = autoSubstitution;
                lineup.UpdatedAt = now;

                ApplyTemplateSlots(lineup, template, assignments);
                var normalizedRoles = NormalizeRoles(template, assignments, request.Roles);
                ApplyRoles(lineup, normalizedRoles);

                var otherLineups = await _dbContext.TeamLineups
                    .Where(l => l.TeamId == teamId && l.LineupId != lineupId)
                    .ToListAsync(ct);

                var shouldActivate = request.IsActive;
                if (!shouldActivate && !otherLineups.Any(l => l.IsActive))
                    shouldActivate = true;

                if (shouldActivate)
                {
                    lineup.IsActive = true;
                    foreach (var other in otherLineups)
                    {
                        if (other.IsActive) { other.IsActive = false; other.UpdatedAt = now; }
                    }
                }
                else if (lineup.IsActive && !request.IsActive)
                {
                    var fallback = otherLineups
                        .OrderByDescending(l => l.IsActive)
                        .ThenByDescending(l => l.UpdatedAt)
                        .FirstOrDefault();

                    if (fallback is null) { lineup.IsActive = true; }
                    else
                    {
                        lineup.IsActive = false;
                        if (!fallback.IsActive) { fallback.IsActive = true; fallback.UpdatedAt = now; }
                    }
                }

                if (request.OffensiveInstructions is not null)
                {
                    var existing = await _dbContext.TeamLineupOffensiveInstructions
                        .FirstOrDefaultAsync(x => x.LineupId == lineupId, ct);
                    if (existing is null)
                        await _dbContext.TeamLineupOffensiveInstructions.AddAsync(BuildOffensiveInstructions(lineupId, request.OffensiveInstructions), ct);
                    else
                        ApplyOffensiveInstructions(existing, request.OffensiveInstructions);
                }

                if (request.DefensiveInstructions is not null)
                {
                    var existing = await _dbContext.TeamLineupDefensiveInstructions
                        .FirstOrDefaultAsync(x => x.LineupId == lineupId, ct);
                    if (existing is null)
                        await _dbContext.TeamLineupDefensiveInstructions.AddAsync(BuildDefensiveInstructions(lineupId, request.DefensiveInstructions), ct);
                    else
                        ApplyDefensiveInstructions(existing, request.DefensiveInstructions);
                }

                if (request.AdvancedInstructions is not null)
                {
                    var existing = await _dbContext.TeamLineupAdvancedInstructions
                        .FirstOrDefaultAsync(x => x.LineupId == lineupId, ct);
                    if (existing is null)
                        await _dbContext.TeamLineupAdvancedInstructions.AddAsync(BuildAdvancedInstructions(lineupId, request.AdvancedInstructions), ct);
                    else
                        ApplyAdvancedInstructions(existing, request.AdvancedInstructions);
                }

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (InvalidOperationException) { await transaction.RollbackAsync(ct); throw; }
            catch (KeyNotFoundException) { await transaction.RollbackAsync(ct); throw; }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Erro ao atualizar a escalação {LineupId} do time {TeamId}.", lineupId, teamId);
                throw new InvalidOperationException("Não foi possível salvar a escalação.", ex);
            }
        });

        return await LoadLineupDtoAsync(lineupId, ct);
    }

    private static LineupChangeLogDto? BuildLastChange(TeamLineup entity, TeamLineupDto current)
    {
        if (entity.LastSeenSnapshotJson is null) return null;

        TeamLineupDto? baseline;
        try
        {
            baseline = JsonSerializer.Deserialize<TeamLineupDto>(entity.LastSeenSnapshotJson);
        }
        catch (JsonException)
        {
            // Retrato corrompido/formato antigo — ignora silenciosamente, não é crítico.
            return null;
        }

        if (baseline is null) return null;

        var changes = ComputeLineupChanges(baseline, current);
        return changes.Count == 0 ? null : new LineupChangeLogDto(entity.LastSeenAtUtc, changes);
    }

    private static List<LineupChangeEntryDto> ComputeLineupChanges(TeamLineupDto before, TeamLineupDto after)
    {
        var changes = new List<LineupChangeEntryDto>();

        if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal))
            changes.Add(new LineupChangeEntryDto("Geral", "Nome da escalação", before.Name, after.Name));

        if (!string.Equals(before.Formation, after.Formation, StringComparison.Ordinal))
            changes.Add(new LineupChangeEntryDto("Geral", "Formação", before.Formation, after.Formation));

        if (before.AutoSubstitution != after.AutoSubstitution)
            changes.Add(new LineupChangeEntryDto("Geral", "Substituições automáticas",
                LineupLabels.AutoSubstitution(before.AutoSubstitution), LineupLabels.AutoSubstitution(after.AutoSubstitution)));

        DiffSlots("Titular", before.Starters, after.Starters, changes);
        DiffSlots("Reserva", before.Bench, after.Bench, changes);

        DiffRole("Capitão", before.Roles.Captain, after.Roles.Captain, changes);
        DiffRole("Falta curta", before.Roles.ShortFreeKick1, after.Roles.ShortFreeKick1, changes);
        DiffRole("Falta curta 2", before.Roles.ShortFreeKick2, after.Roles.ShortFreeKick2, changes);
        DiffRole("Falta longa", before.Roles.LongFreeKick, after.Roles.LongFreeKick, changes);
        DiffRole("Pênaltis", before.Roles.Penalties, after.Roles.Penalties, changes);
        DiffRole("Escanteio esq.", before.Roles.CornerLeft, after.Roles.CornerLeft, changes);
        DiffRole("Escanteio dir.", before.Roles.CornerRight, after.Roles.CornerRight, changes);
        DiffRole("Ataque 1", before.Roles.AttackingPlayer1, after.Roles.AttackingPlayer1, changes);
        DiffRole("Ataque 2", before.Roles.AttackingPlayer2, after.Roles.AttackingPlayer2, changes);
        DiffRole("Ataque 3", before.Roles.AttackingPlayer3, after.Roles.AttackingPlayer3, changes);

        if (before.OffensiveInstructions is { } bo && after.OffensiveInstructions is { } ao)
        {
            const string cat = "Instrução ofensiva";
            if (bo.OffensiveStyle != ao.OffensiveStyle)
                changes.Add(new LineupChangeEntryDto(cat, "Estilo", LineupLabels.OffensiveStyle(bo.OffensiveStyle), LineupLabels.OffensiveStyle(ao.OffensiveStyle)));
            if (bo.Playmaker != ao.Playmaker)
                changes.Add(new LineupChangeEntryDto(cat, "Provocador", LineupLabels.Playmaker(bo.Playmaker), LineupLabels.Playmaker(ao.Playmaker)));
            if (bo.AttackArea != ao.AttackArea)
                changes.Add(new LineupChangeEntryDto(cat, "Área de ataque", LineupLabels.AttackArea(bo.AttackArea), LineupLabels.AttackArea(ao.AttackArea)));
            if (bo.Positioning != ao.Positioning)
                changes.Add(new LineupChangeEntryDto(cat, "Posicionamento", LineupLabels.Positioning(bo.Positioning), LineupLabels.Positioning(ao.Positioning)));
            if (bo.SupportRange != ao.SupportRange)
                changes.Add(new LineupChangeEntryDto(cat, "Alcance de apoio", bo.SupportRange.ToString(), ao.SupportRange.ToString()));
        }

        if (before.DefensiveInstructions is { } bd && after.DefensiveInstructions is { } ad)
        {
            const string cat = "Instrução defensiva";
            if (bd.DefensiveStyle != ad.DefensiveStyle)
                changes.Add(new LineupChangeEntryDto(cat, "Estilo", LineupLabels.DefensiveStyle(bd.DefensiveStyle), LineupLabels.DefensiveStyle(ad.DefensiveStyle)));
            if (bd.ContainmentArea != ad.ContainmentArea)
                changes.Add(new LineupChangeEntryDto(cat, "Área de contenção", LineupLabels.ContainmentArea(bd.ContainmentArea), LineupLabels.ContainmentArea(ad.ContainmentArea)));
            if (bd.Pressure != ad.Pressure)
                changes.Add(new LineupChangeEntryDto(cat, "Pressão", LineupLabels.Pressure(bd.Pressure), LineupLabels.Pressure(ad.Pressure)));
            if (bd.DefensiveLine != ad.DefensiveLine)
                changes.Add(new LineupChangeEntryDto(cat, "Linha defensiva", bd.DefensiveLine.ToString(), ad.DefensiveLine.ToString()));
            if (bd.Density != ad.Density)
                changes.Add(new LineupChangeEntryDto(cat, "Densidade", bd.Density.ToString(), ad.Density.ToString()));
        }

        if (before.AdvancedInstructions is { } ba && after.AdvancedInstructions is { } aa)
        {
            const string cat = "Instrução avançada";
            if (ba.Attack1 != aa.Attack1 || ba.AttackPlayer1Id != aa.AttackPlayer1Id)
                changes.Add(new LineupChangeEntryDto(cat, "Ataque 1",
                    FormatAdvanced(LineupLabels.AdvancedAttack(ba.Attack1), ba.AttackPlayer1),
                    FormatAdvanced(LineupLabels.AdvancedAttack(aa.Attack1), aa.AttackPlayer1)));
            if (ba.Attack2 != aa.Attack2 || ba.AttackPlayer2Id != aa.AttackPlayer2Id)
                changes.Add(new LineupChangeEntryDto(cat, "Ataque 2",
                    FormatAdvanced(LineupLabels.AdvancedAttack(ba.Attack2), ba.AttackPlayer2),
                    FormatAdvanced(LineupLabels.AdvancedAttack(aa.Attack2), aa.AttackPlayer2)));
            if (ba.Defense1 != aa.Defense1 || ba.DefensePlayer1Id != aa.DefensePlayer1Id)
                changes.Add(new LineupChangeEntryDto(cat, "Defesa 1",
                    FormatAdvanced(LineupLabels.AdvancedDefense(ba.Defense1), ba.DefensePlayer1),
                    FormatAdvanced(LineupLabels.AdvancedDefense(aa.Defense1), aa.DefensePlayer1)));
            if (ba.Defense2 != aa.Defense2 || ba.DefensePlayer2Id != aa.DefensePlayer2Id)
                changes.Add(new LineupChangeEntryDto(cat, "Defesa 2",
                    FormatAdvanced(LineupLabels.AdvancedDefense(ba.Defense2), ba.DefensePlayer2),
                    FormatAdvanced(LineupLabels.AdvancedDefense(aa.Defense2), aa.DefensePlayer2)));
        }

        return changes;
    }

    private static void DiffSlots(string category, IReadOnlyList<TeamLineupSlotDto> before, IReadOnlyList<TeamLineupSlotDto> after, List<LineupChangeEntryDto> changes)
    {
        var beforeByCode = before.ToDictionary(s => s.SlotCode, StringComparer.OrdinalIgnoreCase);
        foreach (var afterSlot in after)
        {
            // Slot novo (mudou de formação, por exemplo) — não há "antes" comparável, então não reporta.
            if (!beforeByCode.TryGetValue(afterSlot.SlotCode, out var beforeSlot)) continue;

            var beforePlayerId = beforeSlot.Player?.PlayerId;
            var afterPlayerId = afterSlot.Player?.PlayerId;
            if (beforePlayerId == afterPlayerId) continue;

            changes.Add(new LineupChangeEntryDto(
                category,
                afterSlot.DisplayName,
                beforeSlot.Player?.Name ?? "Vazio",
                afterSlot.Player?.Name ?? "Vazio"));
        }
    }

    private static void DiffRole(string label, TeamLineupSlotPlayerDto? before, TeamLineupSlotPlayerDto? after, List<LineupChangeEntryDto> changes)
    {
        if (before?.PlayerId == after?.PlayerId) return;

        changes.Add(new LineupChangeEntryDto(
            "Função especial",
            label,
            before?.Name ?? "Melhor na função",
            after?.Name ?? "Melhor na função"));
    }

    private static string FormatAdvanced(string label, TeamLineupSlotPlayerDto? player)
        => player is null ? label : $"{label} — {player.Name}";

    public async Task DeleteLineupAsync(Guid teamId, Guid lineupId, CancellationToken ct)
    {
        if (teamId == Guid.Empty) throw new ArgumentException("Time inválido.", nameof(teamId));
        if (lineupId == Guid.Empty) throw new ArgumentException("Escalação inválida.", nameof(lineupId));

        await EnsureTeamExistsAsync(teamId, ct);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
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
            catch (InvalidOperationException) { await transaction.RollbackAsync(ct); throw; }
            catch (KeyNotFoundException) { await transaction.RollbackAsync(ct); throw; }
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
        if (teamId == Guid.Empty) throw new ArgumentException("Time inválido.", nameof(teamId));
        if (lineupId == Guid.Empty) throw new ArgumentException("Escalação inválida.", nameof(lineupId));

        await EnsureTeamExistsAsync(teamId, ct);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var lineups = await _dbContext.TeamLineups
                    .Where(l => l.TeamId == teamId)
                    .ToListAsync(ct);

                var target = lineups.FirstOrDefault(l => l.LineupId == lineupId)
                    ?? throw new KeyNotFoundException("Escalação não encontrada.");

                var now = _timeProvider.GetUtcNow().UtcDateTime;

                foreach (var l in lineups)
                {
                    var shouldBeActive = l.LineupId == lineupId;
                    if (l.IsActive != shouldBeActive) { l.IsActive = shouldBeActive; l.UpdatedAt = now; }
                }

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (InvalidOperationException) { await transaction.RollbackAsync(ct); throw; }
            catch (KeyNotFoundException) { await transaction.RollbackAsync(ct); throw; }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Erro ao definir escalação ativa {LineupId} do time {TeamId}.", lineupId, teamId);
                throw new InvalidOperationException("Não foi possível definir a escalação ativa.", ex);
            }
        });
    }

    public async Task<TeamLineupDto> DuplicateLineupAsync(Guid teamId, Guid sourceLineupId, CancellationToken ct)
    {
        if (teamId == Guid.Empty) throw new ArgumentException("Time inválido.", nameof(teamId));
        if (sourceLineupId == Guid.Empty) throw new ArgumentException("Escalação inválida.", nameof(sourceLineupId));

        await EnsureTeamExistsAsync(teamId, ct);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        Guid newLineupId = default;
        await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var currentCount = await _dbContext.TeamLineups.CountAsync(l => l.TeamId == teamId, ct);
                if (currentCount >= 3)
                    throw new InvalidOperationException("O time já possui 3 escalações cadastradas.");

                var sourceLineup = await _dbContext.TeamLineups
                    .AsNoTracking()
                    .Include(l => l.Slots)
                    .Include(l => l.OffensiveInstructions)
                    .Include(l => l.DefensiveInstructions)
                    .Include(l => l.AdvancedInstructions)
                    .FirstOrDefaultAsync(l => l.LineupId == sourceLineupId && l.TeamId == teamId, ct)
                    ?? throw new KeyNotFoundException("Escalação não encontrada.");

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var localNow = now.ToLocalTime();
                var newName = $"{sourceLineup.Name} - {localNow:dd/MM/yyyy HH:mm}";

                if (newName.Length > 80)
                {
                    var maxOriginalNameLength = 80 - " - dd/MM/yyyy HH:mm".Length;
                    var truncatedName = sourceLineup.Name.Length > maxOriginalNameLength
                        ? sourceLineup.Name[..maxOriginalNameLength]
                        : sourceLineup.Name;
                    newName = $"{truncatedName} - {localNow:dd/MM/yyyy HH:mm}";
                }

                var newLineup = new TeamLineup
                {
                    LineupId = Guid.NewGuid(),
                    TeamId = teamId,
                    Name = newName,
                    Formation = sourceLineup.Formation,
                    AutoSubstitution = sourceLineup.AutoSubstitution,
                    IsActive = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CaptainPlayerId = sourceLineup.CaptainPlayerId,
                    ShortFreeKick1PlayerId = sourceLineup.ShortFreeKick1PlayerId,
                    ShortFreeKick2PlayerId = sourceLineup.ShortFreeKick2PlayerId,
                    LongFreeKickPlayerId = sourceLineup.LongFreeKickPlayerId,
                    PenaltiesPlayerId = sourceLineup.PenaltiesPlayerId,
                    CornerLeftPlayerId = sourceLineup.CornerLeftPlayerId,
                    CornerRightPlayerId = sourceLineup.CornerRightPlayerId,
                    AttackingPlayer1Id = sourceLineup.AttackingPlayer1Id,
                    AttackingPlayer2Id = sourceLineup.AttackingPlayer2Id,
                    AttackingPlayer3Id = sourceLineup.AttackingPlayer3Id,
                };

                foreach (var sourceSlot in sourceLineup.Slots)
                {
                    newLineup.Slots.Add(new TeamLineupSlot
                    {
                        LineupSlotId = Guid.NewGuid(),
                        LineupId = newLineup.LineupId,
                        SlotCode = sourceSlot.SlotCode,
                        DisplayName = sourceSlot.DisplayName,
                        IsBench = sourceSlot.IsBench,
                        Order = sourceSlot.Order,
                        PlayerId = sourceSlot.PlayerId
                    });
                }

                await _dbContext.TeamLineups.AddAsync(newLineup, ct);

                if (sourceLineup.OffensiveInstructions is not null)
                {
                    await _dbContext.TeamLineupOffensiveInstructions.AddAsync(new TeamLineupOffensiveInstructions
                    {
                        LineupId = newLineup.LineupId,
                        OffensiveStyle = sourceLineup.OffensiveInstructions.OffensiveStyle,
                        Playmaker = sourceLineup.OffensiveInstructions.Playmaker,
                        AttackArea = sourceLineup.OffensiveInstructions.AttackArea,
                        Positioning = sourceLineup.OffensiveInstructions.Positioning,
                        SupportRange = sourceLineup.OffensiveInstructions.SupportRange,
                    }, ct);
                }

                if (sourceLineup.DefensiveInstructions is not null)
                {
                    await _dbContext.TeamLineupDefensiveInstructions.AddAsync(new TeamLineupDefensiveInstructions
                    {
                        LineupId = newLineup.LineupId,
                        DefensiveStyle = sourceLineup.DefensiveInstructions.DefensiveStyle,
                        ContainmentArea = sourceLineup.DefensiveInstructions.ContainmentArea,
                        Pressure = sourceLineup.DefensiveInstructions.Pressure,
                        DefensiveLine = sourceLineup.DefensiveInstructions.DefensiveLine,
                        Density = sourceLineup.DefensiveInstructions.Density,
                    }, ct);
                }

                if (sourceLineup.AdvancedInstructions is not null)
                {
                    await _dbContext.TeamLineupAdvancedInstructions.AddAsync(new TeamLineupAdvancedInstructions
                    {
                        LineupId = newLineup.LineupId,
                        Attack1 = sourceLineup.AdvancedInstructions.Attack1,
                        AttackPlayer1Id = sourceLineup.AdvancedInstructions.AttackPlayer1Id,
                        Attack2 = sourceLineup.AdvancedInstructions.Attack2,
                        AttackPlayer2Id = sourceLineup.AdvancedInstructions.AttackPlayer2Id,
                        Defense1 = sourceLineup.AdvancedInstructions.Defense1,
                        DefensePlayer1Id = sourceLineup.AdvancedInstructions.DefensePlayer1Id,
                        Defense2 = sourceLineup.AdvancedInstructions.Defense2,
                        DefensePlayer2Id = sourceLineup.AdvancedInstructions.DefensePlayer2Id,
                    }, ct);
                }

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                newLineupId = newLineup.LineupId;
            }
            catch (InvalidOperationException) { await transaction.RollbackAsync(ct); throw; }
            catch (KeyNotFoundException) { await transaction.RollbackAsync(ct); throw; }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Erro ao duplicar a escalação {LineupId} do time {TeamId}.", sourceLineupId, teamId);
                throw new InvalidOperationException("Não foi possível duplicar a escalação.", ex);
            }
        });

        return await LoadLineupDtoAsync(newLineupId, ct);
    }

    private async Task EnsureTeamExistsAsync(Guid teamId, CancellationToken ct)
    {
        var exists = await _dbContext.Teams.AnyAsync(t => t.TeamId == teamId, ct);
        if (!exists) throw new KeyNotFoundException("Time não encontrado.");
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("O nome da escalação é obrigatório.");

        var trimmed = value.Trim();
        if (trimmed.Length is < 3 or > 80)
            throw new InvalidOperationException("O nome da escalação deve ter entre 3 e 80 caracteres.");

        return trimmed;
    }

    private static int NormalizeAutoSubstitution(int value)
    {
        if (value < 1 || value > 4)
            throw new InvalidOperationException("Substituição automática inválida.");
        return value;
    }

    private static TeamLineupRoleAssignmentsDto NormalizeRoles(
        LineupTemplate template,
        IReadOnlyDictionary<string, int?> assignments,
        TeamLineupRoleAssignmentsDto? roles)
    {
        var starterPlayerIds = template.Starters
            .Select(s => assignments.TryGetValue(s.SlotCode, out var playerId) ? playerId : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        int? Normalize(int? playerId)
            => playerId.HasValue && starterPlayerIds.Contains(playerId.Value) ? playerId : null;

        roles ??= new TeamLineupRoleAssignmentsDto(null, null, null, null, null, null, null, null, null, null);

        return new TeamLineupRoleAssignmentsDto(
            Normalize(roles.CaptainPlayerId),
            Normalize(roles.ShortFreeKick1PlayerId),
            Normalize(roles.ShortFreeKick2PlayerId),
            Normalize(roles.LongFreeKickPlayerId),
            Normalize(roles.PenaltiesPlayerId),
            Normalize(roles.CornerLeftPlayerId),
            Normalize(roles.CornerRightPlayerId),
            Normalize(roles.AttackingPlayer1Id),
            Normalize(roles.AttackingPlayer2Id),
            Normalize(roles.AttackingPlayer3Id));
    }

    private static void ApplyRoles(TeamLineup lineup, TeamLineupRoleAssignmentsDto roles)
    {
        lineup.CaptainPlayerId = roles.CaptainPlayerId;
        lineup.ShortFreeKick1PlayerId = roles.ShortFreeKick1PlayerId;
        lineup.ShortFreeKick2PlayerId = roles.ShortFreeKick2PlayerId;
        lineup.LongFreeKickPlayerId = roles.LongFreeKickPlayerId;
        lineup.PenaltiesPlayerId = roles.PenaltiesPlayerId;
        lineup.CornerLeftPlayerId = roles.CornerLeftPlayerId;
        lineup.CornerRightPlayerId = roles.CornerRightPlayerId;
        lineup.AttackingPlayer1Id = roles.AttackingPlayer1Id;
        lineup.AttackingPlayer2Id = roles.AttackingPlayer2Id;
        lineup.AttackingPlayer3Id = roles.AttackingPlayer3Id;
    }

    private static Dictionary<string, int?> BuildAssignments(LineupTemplate template, TeamLineupSaveRequestDto request)
    {
        var assignments = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);

        void Apply(IEnumerable<TeamLineupSlotAssignmentDto> slots)
        {
            if (slots is null) return;
            foreach (var assignment in slots)
            {
                if (assignment is null || string.IsNullOrWhiteSpace(assignment.SlotCode)) continue;
                template.GetSlot(assignment.SlotCode);
                assignments[assignment.SlotCode.Trim()] = assignment.PlayerId;
            }
        }

        Apply(request.Starters);
        Apply(request.Bench);

        foreach (var slot in template.AllSlots)
        {
            if (!assignments.ContainsKey(slot.SlotCode))
                assignments[slot.SlotCode] = null;
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
            .ToList();

        if (selectedPlayerIds.Count == 0) return;

        var uniquePlayerIds = selectedPlayerIds.Distinct().ToList();

        var rosterPlayers = await _dbContext.TeamRosters
            .AsNoTracking()
            .Where(r => r.TeamId == teamId && uniquePlayerIds.Contains(r.PlayerId))
            .Select(r => new { r.PlayerId, r.Player.Name, r.Player.PositionId })
            .ToListAsync(ct);

        if (rosterPlayers.Count != uniquePlayerIds.Count)
            throw new InvalidOperationException("Alguns jogadores informados não pertencem ao elenco da equipe.");

        var rosterNames = rosterPlayers.ToDictionary(x => x.PlayerId, x => x.Name);
        EnsureUniquePlayers(selectedPlayerIds, rosterNames!);

        var rosterPositions = rosterPlayers.ToDictionary(x => x.PlayerId, x => x.PositionId);

        foreach (var (slotCode, playerId) in assignments)
        {
            if (!playerId.HasValue) continue;
            var slot = template.GetSlot(slotCode);
            if (!slot.IsBench && slot.AllowedPositionIds.Count > 0)
            {
                var positionId = rosterPositions[playerId.Value];
                if (!slot.AllowedPositionIds.Contains(positionId))
                    throw new InvalidOperationException($"O jogador selecionado não pode atuar na posição '{slot.DisplayName}'.");
            }
        }
    }

    private static void EnsureUniquePlayers(IReadOnlyCollection<int> selectedPlayerIds, IReadOnlyDictionary<int, string?> rosterNames)
    {
        var duplicates = selectedPlayerIds
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count == 0) return;

        var duplicateNames = duplicates
            .Select(id => rosterNames.TryGetValue(id, out var name) ? name : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .ToList();

        var message = duplicateNames.Count switch
        {
            0 => "Um jogador não pode ocupar mais de uma posição na escalação.",
            1 => $"O jogador {duplicateNames[0]} não pode ocupar mais de uma posição na escalação.",
            _ => $"Os jogadores {string.Join(", ", duplicateNames)} não podem ocupar mais de uma posição na escalação."
        };

        throw new InvalidOperationException(message);
    }

    private void ApplyTemplateSlots(TeamLineup lineup, LineupTemplate template, IReadOnlyDictionary<string, int?> assignments)
    {
        var existingByCode = lineup.Slots.ToDictionary(s => s.SlotCode, StringComparer.OrdinalIgnoreCase);
        var templateCodes = new HashSet<string>(template.AllSlots.Select(s => s.SlotCode), StringComparer.OrdinalIgnoreCase);

        // Remove slots that no longer belong to the new formation directly via DbSet,
        // avoiding navigation-collection fixup that can corrupt change-tracker state.
        foreach (var slot in lineup.Slots.Where(s => !templateCodes.Contains(s.SlotCode)).ToList())
        {
            _dbContext.TeamLineupSlots.Remove(slot);
        }

        foreach (var slotTemplate in template.AllSlots)
        {
            assignments.TryGetValue(slotTemplate.SlotCode, out var playerId);

            if (existingByCode.TryGetValue(slotTemplate.SlotCode, out var slot))
            {
                // Update properties that can legitimately change between formations.
                slot.DisplayName = slotTemplate.DisplayName;
                slot.IsBench = slotTemplate.IsBench;
                slot.Order = slotTemplate.Order;
                slot.PlayerId = playerId;
            }
            else
            {
                // Add new slot directly via DbSet to guarantee Added state (not Modified).
                _dbContext.TeamLineupSlots.Add(new TeamLineupSlot
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
    }

    private static TeamLineupOffensiveInstructions BuildOffensiveInstructions(Guid lineupId, TeamLineupOffensiveInstructionsDto dto)
    {
        return new TeamLineupOffensiveInstructions
        {
            LineupId = lineupId,
            OffensiveStyle = dto.OffensiveStyle,
            Playmaker = dto.Playmaker,
            AttackArea = dto.AttackArea,
            Positioning = dto.Positioning,
            SupportRange = dto.SupportRange,
        };
    }

    private static void ApplyOffensiveInstructions(TeamLineupOffensiveInstructions entity, TeamLineupOffensiveInstructionsDto dto)
    {
        entity.OffensiveStyle = dto.OffensiveStyle;
        entity.Playmaker = dto.Playmaker;
        entity.AttackArea = dto.AttackArea;
        entity.Positioning = dto.Positioning;
        entity.SupportRange = dto.SupportRange;
    }

    private static TeamLineupDefensiveInstructions BuildDefensiveInstructions(Guid lineupId, TeamLineupDefensiveInstructionsDto dto)
    {
        return new TeamLineupDefensiveInstructions
        {
            LineupId = lineupId,
            DefensiveStyle = dto.DefensiveStyle,
            ContainmentArea = dto.ContainmentArea,
            Pressure = dto.Pressure,
            DefensiveLine = dto.DefensiveLine,
            Density = dto.Density,
        };
    }

    private static void ApplyDefensiveInstructions(TeamLineupDefensiveInstructions entity, TeamLineupDefensiveInstructionsDto dto)
    {
        entity.DefensiveStyle = dto.DefensiveStyle;
        entity.ContainmentArea = dto.ContainmentArea;
        entity.Pressure = dto.Pressure;
        entity.DefensiveLine = dto.DefensiveLine;
        entity.Density = dto.Density;
    }

    private static TeamLineupAdvancedInstructions BuildAdvancedInstructions(Guid lineupId, TeamLineupAdvancedInstructionsSaveDto dto)
    {
        return new TeamLineupAdvancedInstructions
        {
            LineupId = lineupId,
            Attack1 = dto.Attack1,
            AttackPlayer1Id = dto.AttackPlayer1Id,
            Attack2 = dto.Attack2,
            AttackPlayer2Id = dto.AttackPlayer2Id,
            Defense1 = dto.Defense1,
            DefensePlayer1Id = dto.DefensePlayer1Id,
            Defense2 = dto.Defense2,
            DefensePlayer2Id = dto.DefensePlayer2Id,
        };
    }

    private static void ApplyAdvancedInstructions(TeamLineupAdvancedInstructions entity, TeamLineupAdvancedInstructionsSaveDto dto)
    {
        entity.Attack1 = dto.Attack1;
        entity.AttackPlayer1Id = dto.AttackPlayer1Id;
        entity.Attack2 = dto.Attack2;
        entity.AttackPlayer2Id = dto.AttackPlayer2Id;
        entity.Defense1 = dto.Defense1;
        entity.DefensePlayer1Id = dto.DefensePlayer1Id;
        entity.Defense2 = dto.Defense2;
        entity.DefensePlayer2Id = dto.DefensePlayer2Id;
    }

    private async Task<TeamLineupDto> LoadLineupDtoAsync(Guid lineupId, CancellationToken ct)
    {
        var entity = await _dbContext.TeamLineups
            .AsNoTracking()
            .Include(l => l.Slots)
                .ThenInclude(s => s.Player)
                    .ThenInclude(p => p!.Position)
            .Include(l => l.CaptainPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.ShortFreeKick1Player).ThenInclude(p => p!.Position)
            .Include(l => l.ShortFreeKick2Player).ThenInclude(p => p!.Position)
            .Include(l => l.LongFreeKickPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.PenaltiesPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.CornerLeftPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.CornerRightPlayer).ThenInclude(p => p!.Position)
            .Include(l => l.AttackingPlayer1).ThenInclude(p => p!.Position)
            .Include(l => l.AttackingPlayer2).ThenInclude(p => p!.Position)
            .Include(l => l.AttackingPlayer3).ThenInclude(p => p!.Position)
            .Include(l => l.OffensiveInstructions)
            .Include(l => l.DefensiveInstructions)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.AttackPlayer1).ThenInclude(p => p!.Position)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.AttackPlayer2).ThenInclude(p => p!.Position)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.DefensePlayer1).ThenInclude(p => p!.Position)
            .Include(l => l.AdvancedInstructions).ThenInclude(a => a!.DefensePlayer2).ThenInclude(p => p!.Position)
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
        var roles = MapRoles(entity);

        TeamLineupOffensiveInstructionsDto? offensiveInstructions = null;
        if (entity.OffensiveInstructions is not null)
        {
            var oi = entity.OffensiveInstructions;
            offensiveInstructions = new TeamLineupOffensiveInstructionsDto(
                oi.OffensiveStyle, oi.Playmaker, oi.AttackArea, oi.Positioning, oi.SupportRange);
        }

        TeamLineupDefensiveInstructionsDto? defensiveInstructions = null;
        if (entity.DefensiveInstructions is not null)
        {
            var di = entity.DefensiveInstructions;
            defensiveInstructions = new TeamLineupDefensiveInstructionsDto(
                di.DefensiveStyle, di.ContainmentArea, di.Pressure, di.DefensiveLine, di.Density);
        }

        TeamLineupAdvancedInstructionsDto? advancedInstructions = null;
        if (entity.AdvancedInstructions is not null)
        {
            var ai = entity.AdvancedInstructions;
            advancedInstructions = new TeamLineupAdvancedInstructionsDto(
                ai.Attack1, ai.AttackPlayer1Id,
                ai.Attack2, ai.AttackPlayer2Id,
                ai.Defense1, ai.DefensePlayer1Id,
                ai.Defense2, ai.DefensePlayer2Id,
                MapRolePlayer(entity, ai.AttackPlayer1Id, ai.AttackPlayer1),
                MapRolePlayer(entity, ai.AttackPlayer2Id, ai.AttackPlayer2),
                MapRolePlayer(entity, ai.DefensePlayer1Id, ai.DefensePlayer1),
                MapRolePlayer(entity, ai.DefensePlayer2Id, ai.DefensePlayer2));
        }

        return new TeamLineupDto(
            entity.LineupId,
            entity.TeamId,
            entity.Name,
            template.Formation,
            entity.AutoSubstitution,
            entity.IsActive,
            starters,
            bench,
            entity.CreatedAt,
            entity.UpdatedAt,
            roles,
            offensiveInstructions,
            defensiveInstructions,
            advancedInstructions);
    }

    private static TeamLineupRolesDto MapRoles(TeamLineup entity)
    {
        return new TeamLineupRolesDto(
            MapRolePlayer(entity, entity.CaptainPlayerId, entity.CaptainPlayer),
            MapRolePlayer(entity, entity.ShortFreeKick1PlayerId, entity.ShortFreeKick1Player),
            MapRolePlayer(entity, entity.ShortFreeKick2PlayerId, entity.ShortFreeKick2Player),
            MapRolePlayer(entity, entity.LongFreeKickPlayerId, entity.LongFreeKickPlayer),
            MapRolePlayer(entity, entity.PenaltiesPlayerId, entity.PenaltiesPlayer),
            MapRolePlayer(entity, entity.CornerLeftPlayerId, entity.CornerLeftPlayer),
            MapRolePlayer(entity, entity.CornerRightPlayerId, entity.CornerRightPlayer),
            MapRolePlayer(entity, entity.AttackingPlayer1Id, entity.AttackingPlayer1),
            MapRolePlayer(entity, entity.AttackingPlayer2Id, entity.AttackingPlayer2),
            MapRolePlayer(entity, entity.AttackingPlayer3Id, entity.AttackingPlayer3));
    }

    private static TeamLineupSlotPlayerDto? MapRolePlayer(TeamLineup entity, int? playerId, Player? navigation)
    {
        if (!playerId.HasValue) return null;

        var player = navigation ?? entity.Slots.FirstOrDefault(s => s.PlayerId == playerId)?.Player;
        if (player is null) return null;

        return new TeamLineupSlotPlayerDto(
            player.PlayerId, player.PlayerGuid, player.Name, player.Position.Name, player.PositionId);
    }

    private static AdminLineupOverviewDto MapToAdminDto(TeamLineup entity)
    {
        var dto = MapToDto(entity);
        var lastChange = BuildLastChange(entity, dto);

        return new AdminLineupOverviewDto(
            dto.LineupId,
            dto.TeamId,
            entity.Team.TeamName,
            dto.Name,
            dto.Formation,
            dto.AutoSubstitution,
            dto.IsActive,
            dto.Starters,
            dto.Bench,
            dto.Roles,
            dto.OffensiveInstructions,
            dto.DefensiveInstructions,
            dto.AdvancedInstructions,
            dto.CreatedAtUtc,
            dto.UpdatedAtUtc,
            lastChange);
    }
}
