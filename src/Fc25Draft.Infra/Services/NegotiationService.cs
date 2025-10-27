using System.Collections.Generic;
using System.Data;
using System.Linq;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class NegotiationService : INegotiationService
{
    private static readonly string[] ActiveStatuses = new[] { "PENDING", "ACCEPTED" };

    private readonly DraftDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public NegotiationService(DraftDbContext dbContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Negotiation> CreateAsync(NegotiationCreateDto dto, CancellationToken ct)
    {
        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        var origemTeam = await ResolveTeamByTokenAsync(dto.TokenOrigem, "tokenOrigem", ct).ConfigureAwait(false);
        var destinoTeam = await ResolveTeamByTokenAsync(dto.TokenDestino, "tokenDestino", ct).ConfigureAwait(false);

        if (origemTeam.TeamId == destinoTeam.TeamId)
        {
            throw new NegotiationValidationException("Os times de origem e destino devem ser diferentes.");
        }

        var tipo = NormalizeTipo(dto.Tipo);
        var observacao = string.IsNullOrWhiteSpace(dto.Observacao)
            ? null
            : dto.Observacao.Trim();

        var valor = NormalizeValor(tipo, dto.ValorOferecido);

        var jogadoresOrigem = NormalizeJogadores(dto.JogadoresOrigem);
        var jogadoresDestino = NormalizeJogadores(dto.JogadoresDestino);

        if (jogadoresOrigem.Count == 0)
        {
            throw new NegotiationValidationException("Ao menos um jogador de origem deve ser informado.");
        }

        if (tipo == "SALE")
        {
            if (jogadoresOrigem.Count != 1)
            {
                throw new NegotiationValidationException("Vendas diretas devem conter exatamente um jogador de origem.");
            }

            if (jogadoresDestino.Count > 0)
            {
                throw new NegotiationValidationException("Vendas diretas não devem informar jogadores de destino.");
            }
        }
        else if (tipo == "TRADE" && jogadoresDestino.Count == 0)
        {
            throw new NegotiationValidationException("Trocas devem conter ao menos um jogador de destino.");
        }

        EnsureNoDuplicatePlayers(jogadoresOrigem, jogadoresDestino);

        await EnsurePlayersBelongToTeamAsync(jogadoresOrigem, origemTeam.TeamId, ct).ConfigureAwait(false);
        await EnsurePlayersBelongToTeamAsync(jogadoresDestino, destinoTeam.TeamId, ct).ConfigureAwait(false);

        await EnsurePlayersNotInOtherNegotiationsAsync(
            jogadoresOrigem.Concat(jogadoresDestino),
            null,
            ct).ConfigureAwait(false);

        if (valor.HasValue && valor.Value > 0)
        {
            var saldoDisponivel = await GetSaldoDisponivelAsync(destinoTeam.TeamId, ct).ConfigureAwait(false);
            if (saldoDisponivel < valor.Value)
            {
                throw new NegotiationConflictException("Saldo insuficiente para realizar a proposta.");
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var negotiation = new Negotiation
        {
            NegotiationId = Guid.NewGuid(),
            OrigemTeamId = origemTeam.TeamId,
            DestinoTeamId = destinoTeam.TeamId,
            ValorOferecido = valor,
            DataInicioUtc = now,
            Status = "PENDING",
            Tipo = tipo,
            Observacao = observacao
        };

        foreach (var playerId in jogadoresOrigem)
        {
            negotiation.Players.Add(new NegotiationPlayer
            {
                NegotiationPlayerId = Guid.NewGuid(),
                NegotiationId = negotiation.NegotiationId,
                PlayerId = playerId,
                TeamId = origemTeam.TeamId,
                Papel = "OFFERED"
            });
        }

        foreach (var playerId in jogadoresDestino)
        {
            negotiation.Players.Add(new NegotiationPlayer
            {
                NegotiationPlayerId = Guid.NewGuid(),
                NegotiationId = negotiation.NegotiationId,
                PlayerId = playerId,
                TeamId = destinoTeam.TeamId,
                Papel = "REQUESTED"
            });
        }

        _dbContext.Negotiations.Add(negotiation);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return await LoadNegotiationAsync(negotiation.NegotiationId, ct).ConfigureAwait(false)
            ?? throw new NegotiationNotFoundException("Negociação não encontrada após criação.");
    }

    public async Task<Negotiation> RespondAsync(Guid negotiationId, NegotiationResponseDto dto, CancellationToken ct)
    {
        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        var negotiation = await LoadNegotiationForUpdateAsync(negotiationId, ct).ConfigureAwait(false)
            ?? throw new NegotiationNotFoundException("Negociação não encontrada.");

        var responderTeam = await ResolveTeamByTokenAsync(dto.Token, "token", ct).ConfigureAwait(false);
        if (responderTeam.TeamId != negotiation.DestinoTeamId)
        {
            throw new UnauthorizedAccessException("Apenas o time de destino pode responder a negociação.");
        }

        var action = NormalizeResponseAction(dto.Acao);
        if (action == "ACCEPT")
        {
            await AcceptAndCompleteAsync(negotiation, ct).ConfigureAwait(false);
        }
        else if (action == "REJECT")
        {
            if (!string.Equals(negotiation.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                throw new NegotiationConflictException("A negociação não está pendente para rejeição.");
            }

            negotiation.Status = "REJECTED";
            negotiation.DataFechamentoUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return await LoadNegotiationAsync(negotiationId, ct).ConfigureAwait(false)
            ?? throw new NegotiationNotFoundException("Negociação não encontrada após atualização.");
    }

    public async Task CancelAsync(Guid negotiationId, Guid teamId, CancellationToken ct)
    {
        var negotiation = await LoadNegotiationForUpdateAsync(negotiationId, ct).ConfigureAwait(false)
            ?? throw new NegotiationNotFoundException("Negociação não encontrada.");

        if (negotiation.OrigemTeamId != teamId)
        {
            throw new UnauthorizedAccessException("Apenas o time criador pode cancelar a negociação.");
        }

        if (!string.Equals(negotiation.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            throw new NegotiationConflictException("Somente negociações pendentes podem ser canceladas.");
        }

        negotiation.Status = "CANCELLED";
        negotiation.DataFechamentoUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Negotiation>> GetActiveAsync(CancellationToken ct)
    {
        var negotiations = await _dbContext.Negotiations
            .AsNoTracking()
            .Include(n => n.OrigemTeam)
            .Include(n => n.DestinoTeam)
            .Include(n => n.Players)
                .ThenInclude(p => p.Player)
            .Include(n => n.Players)
                .ThenInclude(p => p.Team)
            .Where(n => ActiveStatuses.Contains(n.Status))
            .OrderBy(n => n.DataInicioUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return negotiations;
    }

    public async Task ForceActionAsync(Guid negotiationId, string action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new NegotiationValidationException("Ação obrigatória.");
        }

        var normalizedAction = action.Trim().ToUpperInvariant();
        if (normalizedAction is not ("ACCEPT" or "COMPLETE" or "CANCEL"))
        {
            throw new NegotiationValidationException("Ação inválida para força administrativa.");
        }

        var negotiation = await LoadNegotiationForUpdateAsync(negotiationId, ct).ConfigureAwait(false)
            ?? throw new NegotiationNotFoundException("Negociação não encontrada.");

        switch (normalizedAction)
        {
            case "ACCEPT":
                await AcceptAndCompleteAsync(negotiation, ct).ConfigureAwait(false);
                break;
            case "COMPLETE":
                if (string.Equals(negotiation.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                await AcceptAndCompleteAsync(negotiation, ct).ConfigureAwait(false);
                break;
            case "CANCEL":
                if (string.Equals(negotiation.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NegotiationConflictException("Não é possível cancelar uma negociação concluída.");
                }

                negotiation.Status = "CANCELLED";
                negotiation.DataFechamentoUtc = _timeProvider.GetUtcNow().UtcDateTime;
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task AcceptAndCompleteAsync(Negotiation negotiation, CancellationToken ct)
    {
        if (!string.Equals(negotiation.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(negotiation.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new NegotiationConflictException("Negociação não está em estado pendente para aceitação.");
        }

        var playerIds = negotiation.Players
            .Select(p => p.PlayerId)
            .ToList();

        await EnsurePlayersNotInOtherNegotiationsAsync(playerIds, negotiation.NegotiationId, ct).ConfigureAwait(false);

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct)
            .ConfigureAwait(false);

        var offeredPlayers = negotiation.Players
            .Where(p => string.Equals(p.Papel, "OFFERED", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.PlayerId)
            .ToList();

        var requestedPlayers = negotiation.Players
            .Where(p => string.Equals(p.Papel, "REQUESTED", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.PlayerId)
            .ToList();

        var rosterEntries = await _dbContext.TeamRosters
            .Where(r => playerIds.Contains(r.PlayerId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        EnsureRosterOwnership(rosterEntries, offeredPlayers, negotiation.OrigemTeamId, "origem");
        EnsureRosterOwnership(rosterEntries, requestedPlayers, negotiation.DestinoTeamId, "destino");

        var valor = negotiation.ValorOferecido.GetValueOrDefault();
        if (valor > 0)
        {
            var saldoDisponivel = await GetSaldoDisponivelAsync(negotiation.DestinoTeamId, ct).ConfigureAwait(false);
            if (saldoDisponivel < valor)
            {
                throw new NegotiationConflictException("Saldo insuficiente para concluir a negociação.");
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var roster in rosterEntries
                     .Where(r => r.TeamId == negotiation.OrigemTeamId && offeredPlayers.Contains(r.PlayerId))
                     .ToList())
        {
            _dbContext.TeamRosters.Remove(roster);
        }

        foreach (var roster in rosterEntries
                     .Where(r => r.TeamId == negotiation.DestinoTeamId && requestedPlayers.Contains(r.PlayerId))
                     .ToList())
        {
            _dbContext.TeamRosters.Remove(roster);
        }

        foreach (var playerId in offeredPlayers)
        {
            _dbContext.TeamRosters.Add(new TeamRoster
            {
                TeamId = negotiation.DestinoTeamId,
                PlayerId = playerId
            });
        }

        foreach (var playerId in requestedPlayers)
        {
            _dbContext.TeamRosters.Add(new TeamRoster
            {
                TeamId = negotiation.OrigemTeamId,
                PlayerId = playerId
            });
        }

        if (valor > 0)
        {
            await ApplyBudgetAdjustmentsAsync(negotiation, valor, now, ct).ConfigureAwait(false);
        }

        await RegisterTransferHistoryAsync(negotiation, offeredPlayers, requestedPlayers, valor, now, ct)
            .ConfigureAwait(false);

        negotiation.Status = "COMPLETED";
        negotiation.DataFechamentoUtc = now;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private async Task ApplyBudgetAdjustmentsAsync(Negotiation negotiation, decimal valor, DateTime dataUtc, CancellationToken ct)
    {
        var destinoBudget = await _dbContext.TeamBudgets
            .FirstOrDefaultAsync(tb => tb.TeamId == negotiation.DestinoTeamId, ct)
            .ConfigureAwait(false);

        if (destinoBudget is null)
        {
            destinoBudget = new TeamBudget
            {
                TeamId = negotiation.DestinoTeamId,
                Saldo = 0m
            };

            await _dbContext.TeamBudgets.AddAsync(destinoBudget, ct).ConfigureAwait(false);
        }

        var origemBudget = await _dbContext.TeamBudgets
            .FirstOrDefaultAsync(tb => tb.TeamId == negotiation.OrigemTeamId, ct)
            .ConfigureAwait(false);

        if (origemBudget is null)
        {
            origemBudget = new TeamBudget
            {
                TeamId = negotiation.OrigemTeamId,
                Saldo = 0m
            };

            await _dbContext.TeamBudgets.AddAsync(origemBudget, ct).ConfigureAwait(false);
        }

        destinoBudget.Saldo -= valor;
        origemBudget.Saldo += valor;

        var descricao = $"Negociação {negotiation.NegotiationId}";

        var debitEntry = new BudgetLedger
        {
            BudgetLedgerId = Guid.NewGuid(),
            TeamId = negotiation.DestinoTeamId,
            DataUtc = dataUtc,
            Tipo = "DEBIT",
            Origem = "NEGOTIATION",
            Valor = valor,
            Descricao = descricao
        };

        var creditEntry = new BudgetLedger
        {
            BudgetLedgerId = Guid.NewGuid(),
            TeamId = negotiation.OrigemTeamId,
            DataUtc = dataUtc,
            Tipo = "CREDIT",
            Origem = "NEGOTIATION",
            Valor = valor,
            Descricao = descricao
        };

        await _dbContext.BudgetLedgers.AddRangeAsync(new[] { debitEntry, creditEntry }, ct).ConfigureAwait(false);
    }

    private async Task RegisterTransferHistoryAsync(
        Negotiation negotiation,
        IReadOnlyCollection<int> offeredPlayers,
        IReadOnlyCollection<int> requestedPlayers,
        decimal valor,
        DateTime dataUtc,
        CancellationToken ct)
    {
        var tipo = negotiation.Tipo == "SALE" ? "TEAM_SALE" : "TEAM_TRADE";
        var observacao = negotiation.Observacao;

        var histories = new List<TransferHistory>();

        foreach (var playerId in offeredPlayers)
        {
            histories.Add(new TransferHistory
            {
                TransferHistoryId = Guid.NewGuid(),
                PlayerId = playerId,
                OrigemTeamId = negotiation.OrigemTeamId,
                DestinoTeamId = negotiation.DestinoTeamId,
                Valor = negotiation.Tipo == "SALE" ? valor : 0m,
                Tipo = tipo,
                DataUtc = dataUtc,
                Observacao = observacao
            });
        }

        foreach (var playerId in requestedPlayers)
        {
            histories.Add(new TransferHistory
            {
                TransferHistoryId = Guid.NewGuid(),
                PlayerId = playerId,
                OrigemTeamId = negotiation.DestinoTeamId,
                DestinoTeamId = negotiation.OrigemTeamId,
                Valor = 0m,
                Tipo = tipo,
                DataUtc = dataUtc,
                Observacao = observacao
            });
        }

        if (histories.Count > 0)
        {
            await _dbContext.TransferHistories.AddRangeAsync(histories, ct).ConfigureAwait(false);
        }
    }

    private async Task EnsurePlayersNotInOtherNegotiationsAsync(IEnumerable<int> playerIds, Guid? excludeNegotiationId, CancellationToken ct)
    {
        var ids = playerIds.ToHashSet();
        if (ids.Count == 0)
        {
            return;
        }

        var conflicts = await _dbContext.NegotiationPlayers
            .AsNoTracking()
            .Include(np => np.Negotiation)
            .Where(np => ids.Contains(np.PlayerId)
                && np.NegotiationId != excludeNegotiationId
                && ActiveStatuses.Contains(np.Negotiation.Status))
            .Select(np => np.PlayerId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (conflicts.Count > 0)
        {
            throw new NegotiationConflictException($"Jogador {conflicts.First()} já participa de outra negociação ativa.");
        }
    }

    private static void EnsureRosterOwnership(
        List<TeamRoster> rosterEntries,
        IReadOnlyCollection<int> playerIds,
        Guid expectedTeamId,
        string role)
    {
        if (playerIds.Count == 0)
        {
            return;
        }

        var missing = playerIds
            .Where(playerId => rosterEntries.All(r => r.PlayerId != playerId || r.TeamId != expectedTeamId))
            .ToList();

        if (missing.Count > 0)
        {
            throw new NegotiationConflictException($"Jogador {missing.First()} não pertence ao elenco de {role} na negociação.");
        }
    }

    private async Task EnsurePlayersBelongToTeamAsync(IReadOnlyCollection<int> playerIds, Guid teamId, CancellationToken ct)
    {
        if (playerIds.Count == 0)
        {
            return;
        }

        var existing = await _dbContext.TeamRosters
            .AsNoTracking()
            .Where(r => r.TeamId == teamId && playerIds.Contains(r.PlayerId))
            .Select(r => r.PlayerId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (existing.Count != playerIds.Count)
        {
            var missing = playerIds.Except(existing).First();
            throw new NegotiationValidationException($"Jogador {missing} não pertence ao elenco informado.");
        }
    }

    private static decimal? NormalizeValor(string tipo, decimal? valor)
    {
        if (!valor.HasValue)
        {
            return null;
        }

        var normalized = decimal.Round(valor.Value, 2, MidpointRounding.AwayFromZero);
        if (normalized < 0)
        {
            throw new NegotiationValidationException("Valor oferecido inválido.");
        }

        if (tipo == "SALE" && normalized <= 0)
        {
            throw new NegotiationValidationException("Vendas devem informar um valor maior que zero.");
        }

        return normalized == 0 ? null : normalized;
    }

    private static HashSet<int> NormalizeJogadores(IReadOnlyList<int>? jogadores)
    {
        if (jogadores is null)
        {
            return new HashSet<int>();
        }

        var set = new HashSet<int>();
        foreach (var jogador in jogadores)
        {
            if (!set.Add(jogador))
            {
                throw new NegotiationValidationException("Jogadores duplicados não são permitidos.");
            }
        }

        return set;
    }

    private static void EnsureNoDuplicatePlayers(HashSet<int> origem, HashSet<int> destino)
    {
        if (origem.Count == 0 || destino.Count == 0)
        {
            return;
        }

        if (origem.Overlaps(destino))
        {
            throw new NegotiationValidationException("Um jogador não pode atuar nos dois lados da negociação.");
        }
    }

    private static string NormalizeTipo(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo))
        {
            throw new NegotiationValidationException("Tipo de negociação obrigatório.");
        }

        return tipo.Trim().ToUpperInvariant() switch
        {
            "SALE" => "SALE",
            "TRADE" => "TRADE",
            _ => throw new NegotiationValidationException("Tipo de negociação inválido.")
        };
    }

    private static string NormalizeResponseAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new NegotiationValidationException("Ação obrigatória.");
        }

        return action.Trim().ToUpperInvariant() switch
        {
            "ACCEPT" => "ACCEPT",
            "REJECT" => "REJECT",
            _ => throw new NegotiationValidationException("Ação inválida para resposta.")
        };
    }

    private async Task<Team> ResolveTeamByTokenAsync(string? token, string fieldName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UnauthorizedAccessException($"{fieldName} é obrigatório.");
        }

        if (!Guid.TryParse(token, out var parsedToken))
        {
            throw new UnauthorizedAccessException($"{fieldName} inválido.");
        }

        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(t => t.TeamToken == parsedToken, ct)
            .ConfigureAwait(false);

        return team ?? throw new UnauthorizedAccessException($"{fieldName} inválido.");
    }

    private async Task<Negotiation?> LoadNegotiationAsync(Guid negotiationId, CancellationToken ct)
    {
        return await _dbContext.Negotiations
            .AsNoTracking()
            .Include(n => n.OrigemTeam)
            .Include(n => n.DestinoTeam)
            .Include(n => n.Players)
                .ThenInclude(p => p.Player)
            .Include(n => n.Players)
                .ThenInclude(p => p.Team)
            .FirstOrDefaultAsync(n => n.NegotiationId == negotiationId, ct)
            .ConfigureAwait(false);
    }

    private async Task<Negotiation?> LoadNegotiationForUpdateAsync(Guid negotiationId, CancellationToken ct)
    {
        return await _dbContext.Negotiations
            .Include(n => n.Players)
            .FirstOrDefaultAsync(n => n.NegotiationId == negotiationId, ct)
            .ConfigureAwait(false);
    }

    private async Task<decimal> GetSaldoDisponivelAsync(Guid teamId, CancellationToken ct)
    {
        var saldo = await _dbContext.TeamBudgets
            .AsNoTracking()
            .Where(tb => tb.TeamId == teamId)
            .Select(tb => (decimal?)tb.Saldo)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false) ?? 0m;

        var bloqueado = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(i => i.Status == "OPEN" && i.MaiorLanceTeamId == teamId && i.LanceAtual != null)
            .Select(i => (decimal?)i.LanceAtual)
            .SumAsync(ct)
            .ConfigureAwait(false) ?? 0m;

        return decimal.Round(saldo - bloqueado, 2, MidpointRounding.AwayFromZero);
    }
}
