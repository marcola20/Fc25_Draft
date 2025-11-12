using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
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
    private readonly ILogger<TeamLineupService> _logger;

    public TeamLineupService(
        DraftDbContext db,
        IFormationSlotFactory formationSlotFactory,
        ILogger<TeamLineupService> logger)
    {
        _db = db;
        _formationSlotFactory = formationSlotFactory;
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
                positionNames.TryGetValue(s.PrimaryPositionId, out var name) ? name : $"Posição {s.PrimaryPositionId}",
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
                positionNames.TryGetValue(t.PrimaryPositionId, out var label) ? label : $"Posição {t.PrimaryPositionId}"))
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

        var rosterPlayers = await _db.TeamRosters
            .AsNoTracking()
            .Where(r => r.TeamId == teamId)
            .Select(r => new
            {
                r.PlayerId,
                r.Player.PositionId,
                r.Player.Name
            })
            .ToDictionaryAsync(r => r.PlayerId, ct)
            .ConfigureAwait(false);

        var startersCount = 0;
        var benchCount = 0;
        var gkCount = 0;
        var usedPlayers = new HashSet<int>();

        foreach (var slot in request.Slots)
        {
            if (!expectedByOrder.TryGetValue(slot.Order, out var expected))
            {
                throw new InvalidOperationException($"Slot {slot.Order} não faz parte da formação selecionada.");
            }

            if (slot.Role != expected.Role)
            {
                throw new InvalidOperationException($"Slot {slot.Order} com papel inválido.");
            }

            if (slot.PrimaryPositionId != expected.PrimaryPositionId)
            {
                throw new InvalidOperationException($"Slot {slot.Order} com posição incompatível com a formação.");
            }

            if (slot.PlayerId is null)
            {
                throw new InvalidOperationException("Todos os slots devem possuir um jogador selecionado.");
            }

            var playerId = slot.PlayerId.Value;
            if (!rosterPlayers.TryGetValue(playerId, out var rosterInfo))
            {
                throw new InvalidOperationException($"Jogador {playerId} não pertence ao elenco do time.");
            }

            if (!usedPlayers.Add(playerId))
            {
                throw new InvalidOperationException("Não é permitido repetir jogadores entre titulares e reservas.");
            }

            if (!IsPlayerCompatible(slot.PrimaryPositionId, rosterInfo.PositionId))
            {
                throw new InvalidOperationException($"Jogador {rosterInfo.Name} não é elegível para o slot {slot.Order}.");
            }

            if (slot.Role == 0)
            {
                startersCount++;
                if (rosterInfo.PositionId == 1)
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
            throw new InvalidOperationException("Selecione exatamente 11 titulares.");
        }

        if (benchCount != 7)
        {
            throw new InvalidOperationException("Selecione exatamente 7 reservas.");
        }

        if (gkCount != 1)
        {
            throw new InvalidOperationException("É obrigatório ter exatamente 1 goleiro entre os titulares.");
        }

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

            return await GetActiveAsync(teamId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Falha ao carregar a escalação salva.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            _logger.LogError(ex, "Erro ao salvar escalação do time {TeamId}.", teamId);
            throw;
        }
    }

    private static bool IsPlayerCompatible(int slotPrimaryPositionId, short playerPositionId)
    {
        return slotPrimaryPositionId switch
        {
            1 => playerPositionId == 1,
            2 or 3 or 4 => playerPositionId is 2 or 3 or 4,
            5 or 6 => playerPositionId is 5 or 6,
            7 => playerPositionId is 6 or 7,
            8 or 9 => playerPositionId is 7 or 8 or 9,
            10 => playerPositionId == 10,
            _ => false
        };
    }
}
