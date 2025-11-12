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

        if (lineup is null)
        {
            return null;
        }

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
                positionNames.TryGetValue((short)s.PrimaryPositionId, out var name) ? name : $"Posição {s.PrimaryPositionId}",
                s.Player?.Name))
            .ToList();

        return new TeamLineupResponse(
            lineup.LineupId,
            lineup.TeamId,
            lineup.FormationCode,
            lineup.TacticCode,
            lineup.IsActive,
            lineup.UpdatedAtUtc,
            slotDtos);
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
            {
                _logger.LogWarning("Validação da escalação do time {TeamId} falhou: {Detail}", teamId, detail);
            }
            else
            {
                _logger.LogWarning("Validação da escalação do time {TeamId} falhou: {Message}", teamId, message);
            }

            throw new InvalidOperationException(message);
        }

        foreach (var slot in request.Slots)
        {
            if (!expectedByOrder.TryGetValue(slot.Order, out var expected))
            {
                FailValidation($"Slot {slot.Order} não faz parte da formação selecionada.");
            }

            if (slot.Role != expected.Role)
            {
                FailValidation($"Slot {slot.Order} com papel inválido.");
            }

            if (slot.PrimaryPositionId != expected.PrimaryPositionId)
            {
                FailValidation($"Slot {slot.Order} com posição incompatível com a formação.");
            }

            if (slot.PlayerId is null)
            {
                FailValidation("Todos os slots devem possuir um jogador selecionado.");
            }

            var playerId = slot.PlayerId.Value;
            if (!rosterPlayers.TryGetValue(playerId, out var rosterInfo))
            {
                FailValidation("Jogador não pertence ao time.", $"JogadorId={playerId}");
            }

            if (!usedPlayers.Add(playerId))
            {
                FailValidation("Há jogadores repetidos na escalação.", $"JogadorId={playerId}");
            }

            if (!_eligibilityService.IsEligible(slot.PrimaryPositionId, rosterInfo.PositionId, rosterInfo.SecondaryPositionIds))
            {
                FailValidation("Jogador não elegível para este slot.", $"Jogador={rosterInfo.Name};Slot={slot.Order}");
            }

            if (slot.Role == 0)
            {
                startersCount++;

                if (rosterInfo.PositionId == (int)PositionType.Goleiro)
                {
                    gkCount++;
                }
            }
            else
            {
                benchCount++;
            }
        }

        if (startersCount != 11)
        {
            FailValidation("Selecione exatamente 11 titulares.");
        }

        if (benchCount != 7)
        {
            FailValidation("Selecione exatamente 7 reservas.");
        }

        if (gkCount != 1)
        {
            FailValidation("É obrigatório ter 1 goleiro entre os titulares.");
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
                try
                {
                    var existingLineups = await _db.TeamLineups
                        .Where(l => l.TeamId == teamId && l.IsActive)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    foreach (var lineup in existingLineups)
                    {
                        lineup.IsActive = false;
                    }

                    var now = DateTime.UtcNow;
                    var newLineup = new TeamLineup
                    {
                        LineupId = Guid.NewGuid(),
                        TeamId = teamId,
                        FormationCode = normalizedFormation,
                        TacticCode = tacticCode,
                        IsActive = true,
                        UpdatedAtUtc = now
                    };

                    foreach (var slot in request.Slots.OrderBy(s => s.Order))
                    {
                        newLineup.Slots.Add(new TeamLineupSlot
                        {
                            SlotId = Guid.NewGuid(),
                            LineupId = newLineup.LineupId,
                            Order = slot.Order,
                            Role = slot.Role,
                            PrimaryPositionId = slot.PrimaryPositionId,
                            PlayerId = slot.PlayerId
                        });
                    }

                    _db.TeamLineups.Add(newLineup);
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                }
                catch
                {
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }

                var reloaded = await GetActiveAsync(teamId, ct).ConfigureAwait(false);
                if (reloaded is null)
                {
                    _logger.LogError("Falha ao recarregar a escalação salva do time {TeamId}.", teamId);
                    throw new InvalidOperationException("Ocorreu um erro ao salvar a escalação.");
                }

                return reloaded;
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

    private sealed record RosterPlayer(int PlayerId, short PositionId, string Name, IReadOnlyCollection<int> SecondaryPositionIds);
}
