using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

public sealed class TeamLineupService : ITeamLineupService
{
    private readonly DraftDbContext _db;
    private readonly IFormationSlotFactory _formationSlotFactory;
    private readonly IPositionEligibilityService _eligibilityService;
    private readonly ILogger<TeamLineupService> _logger;

    public TeamLineupService(
        DraftDbContext db,
        IFormationSlotFactory formationSlotFactory,
        IPositionEligibilityService eligibilityService,
        ILogger<TeamLineupService> logger)
    {
        _db = db;
        _formationSlotFactory = formationSlotFactory;
        _eligibilityService = eligibilityService;
        _logger = logger;
    }

    public async Task<TeamLineupResponse?> GetActiveAsync(Guid teamId, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        var lineup = await _db.TeamLineups
            .AsNoTracking()
            .Include(l => l.Slots)
                .ThenInclude(s => s.Player)
            .Where(l => l.TeamId == teamId && l.IsActive)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return lineup is null ? null : await BuildResponseAsync(lineup, ct).ConfigureAwait(false);
    }

    public async Task<TeamLineupResponse?> GetByIdAsync(Guid teamId, Guid lineupId, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (lineupId == Guid.Empty)
        {
            throw new ArgumentException("Escalação inválida.", nameof(lineupId));
        }

        var lineup = await _db.TeamLineups
            .AsNoTracking()
            .Include(l => l.Slots)
                .ThenInclude(s => s.Player)
            .Where(l => l.TeamId == teamId && l.LineupId == lineupId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return lineup is null ? null : await BuildResponseAsync(lineup, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TeamLineupSummaryResponse>> GetSummariesAsync(Guid teamId, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        return await _db.TeamLineups
            .AsNoTracking()
            .Where(l => l.TeamId == teamId)
            .OrderByDescending(l => l.UpdatedAtUtc)
            .Select(l => new TeamLineupSummaryResponse(
                l.LineupId,
                l.Name,
                l.FormationCode,
                l.TacticCode,
                l.IsActive,
                l.UpdatedAtUtc))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LineupSlotTemplateDto>> BuildTemplateAsync(string formationCode, CancellationToken ct)
    {
        var templates = _formationSlotFactory.Build(formationCode);
        var positionIds = templates
            .Select(t => t.PrimaryPositionId)
            .Distinct()
            .ToArray();

        var positionNames = await _db.Positions
            .AsNoTracking()
            .Where(p => positionIds.Contains(p.PositionId))
            .ToDictionaryAsync(p => p.PositionId, p => p.Name, ct)
            .ConfigureAwait(false);

        return templates
            .OrderBy(t => t.Order)
            .Select(t => new LineupSlotTemplateDto(
                t.Order,
                t.Role,
                t.PrimaryPositionId,
                positionNames.TryGetValue((short)t.PrimaryPositionId, out var label) ? label : $"Posição {t.PrimaryPositionId}"))
            .ToList();
    }

    public async Task SetActiveAsync(Guid teamId, Guid lineupId, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
            throw new ArgumentException("Time inválido.", nameof(teamId));

        if (lineupId == Guid.Empty)
            throw new ArgumentException("Escalação inválida.", nameof(lineupId));

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var lineups = await _db.TeamLineups
                    .Where(l => l.TeamId == teamId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var target = lineups.FirstOrDefault(l => l.LineupId == lineupId)
                    ?? throw new KeyNotFoundException("Escalação não encontrada.");

                if (!target.IsActive)
                {
                    target.IsActive = true;
                    foreach (var lineup in lineups.Where(l => l.LineupId != lineupId && l.IsActive))
                    {
                        lineup.IsActive = false;
                    }

                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                await transaction.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
        }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid teamId, Guid lineupId, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
            throw new ArgumentException("Time inválido.", nameof(teamId));

        if (lineupId == Guid.Empty)
            throw new ArgumentException("Escalação inválida.", nameof(lineupId));

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var lineups = await _db.TeamLineups
                    .Where(l => l.TeamId == teamId)
                    .OrderByDescending(l => l.UpdatedAtUtc)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var target = lineups.FirstOrDefault(l => l.LineupId == lineupId)
                    ?? throw new KeyNotFoundException("Escalação não encontrada.");

                _db.TeamLineups.Remove(target);

                if (target.IsActive)
                {
                    var fallback = lineups
                        .Where(l => l.LineupId != lineupId)
                        .OrderByDescending(l => l.UpdatedAtUtc)
                        .FirstOrDefault();

                    if (fallback is not null)
                    {
                        fallback.IsActive = true;
                    }
                }

                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
        }).ConfigureAwait(false);
    }

    public async Task<TeamLineupResponse> SaveAsync(Guid teamId, SaveLineupRequest request, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var normalizedFormation = request.FormationCode?.Trim() ?? string.Empty;
        if (!_formationSlotFactory.Supports(normalizedFormation))
        {
            throw new InvalidOperationException("Formação não suportada.");
        }

        var tacticCode = (request.TacticCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(tacticCode) || tacticCode.Length > 40)
        {
            throw new InvalidOperationException("Código de tática inválido. Informe entre 1 e 40 caracteres.");
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 60)
        {
            throw new InvalidOperationException("Informe um nome para a escalação (até 60 caracteres).");
        }

        var observation = string.IsNullOrWhiteSpace(request.Observation)
            ? null
            : request.Observation!.Trim();

        if (observation is { Length: > 500 })
        {
            throw new InvalidOperationException("Observação deve ter no máximo 500 caracteres.");
        }

        var expectedSlots = _formationSlotFactory.Build(normalizedFormation);
        var expectedByOrder = expectedSlots.ToDictionary(s => s.Order);

        if (request.Slots.Count != expectedSlots.Count)
        {
            throw new InvalidOperationException("Quantidade de slots inválida para a formação selecionada.");
        }

        if (request.Slots.Select(s => s.Order).Distinct().Count() != request.Slots.Count)
        {
            throw new InvalidOperationException("Existem slots duplicados na requisição.");
        }

        if (!await _db.Teams.AsNoTracking().AnyAsync(t => t.TeamId == teamId, ct).ConfigureAwait(false))
        {
            throw new KeyNotFoundException("Time não encontrado.");
        }

        var rosterEntries = await _db.TeamRosters
            .AsNoTracking()
            .Where(r => r.TeamId == teamId)
            .Select(r => new
            {
                r.PlayerId,
                r.Player.PositionId,
                r.Player.Name
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var rosterPlayers = rosterEntries.ToDictionary(
            r => r.PlayerId,
            r => new RosterPlayer(r.PlayerId, r.PositionId, r.Name, Array.Empty<int>()));

        var startersCount = 0;
        var benchCount = 0;
        var gkCount = 0;
        var usedPlayers = new HashSet<int>();

        void FailValidation(string message, string? detail = null)
        {
            if (!string.IsNullOrWhiteSpace(detail))
                _logger.LogWarning("Validação da escalação do time {TeamId} falhou: {Detail}", teamId, detail);
            else
                _logger.LogWarning("Validação da escalação do time {TeamId} falhou: {Message}", teamId, message);

            throw new InvalidOperationException(message);
        }

        foreach (var slot in request.Slots)
        {
            if (!expectedByOrder.TryGetValue(slot.Order, out var expected))
                FailValidation($"Slot {slot.Order} não faz parte da formação selecionada.");

            if (slot.Role != expected.Role)
                FailValidation($"Slot {slot.Order} com papel inválido.");

            if (slot.PrimaryPositionId != expected.PrimaryPositionId)
                FailValidation($"Slot {slot.Order} com posição incompatível com a formação.");

            if (slot.PlayerId is null)
                FailValidation("Todos os slots devem possuir um jogador selecionado.");

            var playerId = slot.PlayerId!.Value;
            if (!rosterPlayers.TryGetValue(playerId, out var rosterInfo))
                FailValidation("Jogador não pertence ao time.", $"JogadorId={playerId}");

            if (!usedPlayers.Add(playerId))
                FailValidation("Há jogadores repetidos na escalação.", $"JogadorId={playerId}");

            if (slot.Role == 0 && !_eligibilityService.IsEligible(slot.PrimaryPositionId, rosterInfo.PositionId, rosterInfo.SecondaryPositionIds))
                FailValidation("Jogador não elegível para este slot.", $"Jogador={rosterInfo.Name};Slot={slot.Order}");

            if (slot.Role == 0)
            {
                startersCount++;

                if (rosterInfo!.PositionId == (int)PositionType.Goleiro)
                    gkCount++;
            }
            else
                benchCount++;
        }

        if (startersCount != 11)
            FailValidation("Selecione exatamente 11 titulares.");

        if (benchCount != 7)
            FailValidation("Selecione exatamente 7 reservas.");

        if (gkCount != 1)
            FailValidation("É obrigatório ter 1 goleiro entre os titulares.");

        var starterIds = request.Slots
            .Where(s => s.Role == 0 && s.PlayerId.HasValue)
            .Select(s => s.PlayerId!.Value)
            .ToHashSet();

        int RequireStarter(int? playerId, string label)
        {
            if (playerId is null)
                FailValidation($"Selecione um jogador titular para {label}.");

            if (!starterIds.Contains(playerId.Value))
                FailValidation($"{label} deve ser preenchido com um dos titulares selecionados.");

            return playerId.Value;
        }

        var specialRoles = request.SpecialRoles ?? new LineupSpecialRolesDto();
        var captainId = RequireStarter(specialRoles.CaptainPlayerId, "Capitão");
        var fkLeftId = RequireStarter(specialRoles.ShortFreeKickLeftPlayerId, "Falta perto (esquerda)");
        var fkRightId = RequireStarter(specialRoles.ShortFreeKickRightPlayerId, "Falta perto (direita)");
        var fkLongId = RequireStarter(specialRoles.LongFreeKickPlayerId, "Falta de longe");
        var penaltyId = RequireStarter(specialRoles.PenaltyKickPlayerId, "Pênaltis");
        var cornerLeftId = RequireStarter(specialRoles.LeftCornerPlayerId, "Escanteio esquerdo");
        var cornerRightId = RequireStarter(specialRoles.RightCornerPlayerId, "Escanteio direito");

        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
                try
                {
                    var existingLineups = await _db.TeamLineups
                        .Include(l => l.Slots)
                        .Where(l => l.TeamId == teamId)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    var isNew = request.LineupId is null;
                    if (isNew && existingLineups.Count >= 3)
                    {
                        throw new InvalidOperationException("O time já possui 3 escalações salvas. Exclua uma escalação antes de salvar uma nova.");
                    }

                    TeamLineup targetLineup;
                    if (isNew)
                    {
                        targetLineup = new TeamLineup
                        {
                            LineupId = Guid.NewGuid(),
                            TeamId = teamId
                        };
                        _db.TeamLineups.Add(targetLineup);
                        existingLineups.Add(targetLineup);
                    }
                    else
                    {
                        targetLineup = existingLineups.FirstOrDefault(l => l.LineupId == request.LineupId)
                            ?? throw new KeyNotFoundException("Escalação não encontrada.");
                    }

                    var requestedSignature = BuildSlotSignature(request.Slots.Select(s => (s.Order, s.PlayerId)));
                    if (existingLineups.Any(l =>
                            l.LineupId != targetLineup.LineupId &&
                            string.Equals(l.FormationCode, normalizedFormation, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(BuildSlotSignature(l.Slots.Select(s => (s.Order, s.PlayerId))), requestedSignature, StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException("Já existe uma escalação com a mesma formação e jogadores nas mesmas posições.");
                    }

                    var now = DateTime.UtcNow;
                    targetLineup.Name = name;
                    targetLineup.FormationCode = normalizedFormation;
                    targetLineup.TacticCode = tacticCode;
                    targetLineup.Observation = observation;
                    targetLineup.CaptainPlayerId = captainId;
                    targetLineup.ShortFreeKickLeftPlayerId = fkLeftId;
                    targetLineup.ShortFreeKickRightPlayerId = fkRightId;
                    targetLineup.LongFreeKickPlayerId = fkLongId;
                    targetLineup.PenaltyKickPlayerId = penaltyId;
                    targetLineup.LeftCornerPlayerId = cornerLeftId;
                    targetLineup.RightCornerPlayerId = cornerRightId;
                    targetLineup.UpdatedAtUtc = now;

                    var shouldActivate = request.SetAsActive || targetLineup.IsActive || !existingLineups.Any(l => l.IsActive && l.LineupId != targetLineup.LineupId);
                    targetLineup.IsActive = shouldActivate;

                    if (targetLineup.IsActive)
                    {
                        foreach (var lineup in existingLineups.Where(l => l.LineupId != targetLineup.LineupId && l.IsActive))
                        {
                            lineup.IsActive = false;
                        }
                    }

                    if (!isNew)
                    {
                        _db.TeamLineupSlots.RemoveRange(targetLineup.Slots);
                        targetLineup.Slots.Clear();
                    }

                    foreach (var slot in request.Slots.OrderBy(s => s.Order))
                    {
                        targetLineup.Slots.Add(new TeamLineupSlot
                        {
                            SlotId = Guid.NewGuid(),
                            LineupId = targetLineup.LineupId,
                            Order = slot.Order,
                            Role = slot.Role,
                            PrimaryPositionId = slot.PrimaryPositionId,
                            PlayerId = slot.PlayerId
                        });
                    }

                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    await transaction.CommitAsync(ct).ConfigureAwait(false);

                    await _db.Entry(targetLineup)
                        .Collection(l => l.Slots)
                        .Query()
                        .Include(s => s.Player)
                        .LoadAsync(ct)
                        .ConfigureAwait(false);

                    return await BuildResponseAsync(targetLineup, ct).ConfigureAwait(false);
                }
                catch
                {
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }
            }).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao salvar escalação do time {TeamId}.", teamId);
            throw new InvalidOperationException("Ocorreu um erro ao salvar a escalação.", ex);
        }
    }

    private async Task<TeamLineupResponse> BuildResponseAsync(TeamLineup lineup, CancellationToken ct)
    {
        var positionIds = lineup.Slots
            .Select(s => s.PrimaryPositionId)
            .Distinct()
            .ToArray();

        var positionNames = await _db.Positions
            .AsNoTracking()
            .Where(p => positionIds.Contains(p.PositionId))
            .ToDictionaryAsync(p => p.PositionId, p => p.Name, ct)
            .ConfigureAwait(false);

        var slotDtos = lineup.Slots
            .OrderBy(s => s.Order)
            .Select(s => new LineupSlotResponse(
                s.SlotId,
                s.Order,
                s.Role,
                s.PrimaryPositionId,
                s.PlayerId,
                positionNames.TryGetValue((short)s.PrimaryPositionId, out var label) ? label : $"Posição {s.PrimaryPositionId}",
                s.Player?.Name))
            .ToList();

        var playerNames = lineup.Slots
            .Where(s => s.PlayerId.HasValue && s.Player is not null)
            .GroupBy(s => s.PlayerId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Player!.Name);

        string? ResolveName(int? playerId) => playerId.HasValue && playerNames.TryGetValue(playerId.Value, out var value)
            ? value
            : null;

        var specialRoles = new LineupSpecialRolesResponse(
            lineup.CaptainPlayerId,
            ResolveName(lineup.CaptainPlayerId),
            lineup.ShortFreeKickLeftPlayerId,
            ResolveName(lineup.ShortFreeKickLeftPlayerId),
            lineup.ShortFreeKickRightPlayerId,
            ResolveName(lineup.ShortFreeKickRightPlayerId),
            lineup.LongFreeKickPlayerId,
            ResolveName(lineup.LongFreeKickPlayerId),
            lineup.PenaltyKickPlayerId,
            ResolveName(lineup.PenaltyKickPlayerId),
            lineup.LeftCornerPlayerId,
            ResolveName(lineup.LeftCornerPlayerId),
            lineup.RightCornerPlayerId,
            ResolveName(lineup.RightCornerPlayerId));

        return new TeamLineupResponse(
            lineup.LineupId,
            lineup.TeamId,
            lineup.Name,
            lineup.FormationCode,
            lineup.TacticCode,
            lineup.Observation,
            lineup.IsActive,
            lineup.UpdatedAtUtc,
            specialRoles,
            slotDtos);
    }

    private static string BuildSlotSignature(IEnumerable<(int Order, int? PlayerId)> slots)
    {
        return string.Join('|', slots
            .OrderBy(s => s.Order)
            .Select(s => $"{s.Order}:{s.PlayerId?.ToString() ?? "-"}"));
    }

    private sealed record RosterPlayer(int PlayerId, short PositionId, string Name, IReadOnlyCollection<int> SecondaryPositionIds);
}
