using System.Security.Cryptography;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Infra.Services;

public class MarketService : IMarketService
{
    private readonly DraftDbContext _dbContext;
    private readonly IPricingService _pricingService;
    private readonly MarketGenerationOptions _options;
    private readonly TimeProvider _timeProvider;

    public MarketService(
        DraftDbContext dbContext,
        IPricingService pricingService,
        IOptions<MarketGenerationOptions> options,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _pricingService = pricingService;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<TransferMarketItem>> GenerateRoundAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var protectionMinutes = Math.Max(0, _options.JanelaProtecaoMinutos);
        var protectionThreshold = nowUtc - TimeSpan.FromMinutes(protectionMinutes);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var hasRecentOpenRound = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .AnyAsync(x => x.Status == "OPEN" && x.DataInicioUtc >= protectionThreshold, ct)
            .ConfigureAwait(false);

        if (hasRecentOpenRound)
        {
            throw new MarketGenerationConflictException(
                $"Já existe uma rodada aberta recentemente (janela de {_options.JanelaProtecaoMinutos} minuto(s)).");
        }

        var excludedPlayerIds = new HashSet<int>(await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(x => x.Status == "OPEN")
            .Select(x => x.PlayerId)
            .ToListAsync(ct)
            .ConfigureAwait(false));

        var selectedPlayers = new List<Player>(_options.QuantidadePorRodada);

        await SelectPlayersForRangeAsync(
            _options.ComunsFaixaMin,
            _options.ComunsFaixaMax,
            _options.ComunsQuantidade,
            excludedPlayerIds,
            selectedPlayers,
            ct).ConfigureAwait(false);

        await SelectPlayersForRangeAsync(
            _options.IntermediarioFaixaMin,
            _options.IntermediarioFaixaMax,
            _options.IntermediarioQuantidade,
            excludedPlayerIds,
            selectedPlayers,
            ct).ConfigureAwait(false);

        await SelectRarePlayersAsync(
            _options.RaroFaixaMin,
            _options.RaroFaixaMax,
            _options.RaroQuantidade,
            excludedPlayerIds,
            selectedPlayers,
            ct).ConfigureAwait(false);

        if (selectedPlayers.Count != _options.QuantidadePorRodada)
        {
            throw new MarketGenerationValidationException(
                $"Configuração inconsistente: esperados {_options.QuantidadePorRodada} jogadores, mas foram selecionados {selectedPlayers.Count}.");
        }

        var items = new List<TransferMarketItem>(selectedPlayers.Count);
        foreach (var player in selectedPlayers)
        {
            var pricing = await _pricingService.CalculateForPlayerAsync(player.PlayerId, ct).ConfigureAwait(false);

            var item = new TransferMarketItem
            {
                MarketItemId = Guid.NewGuid(),
                PlayerId = player.PlayerId,
                PrecoBase = pricing.PrecoBase,
                LanceAtual = null,
                MaiorLanceTeamId = null,
                PrecoComprarAgora = pricing.ComprarAgora,
                DataInicioUtc = nowUtc,
                DataFimUtc = null,
                Status = "OPEN",
                VencedorTeamId = null
            };

            items.Add(item);
        }

        await _dbContext.TransferMarketItems.AddRangeAsync(items, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        var lookup = selectedPlayers.ToDictionary(p => p.PlayerId);
        foreach (var item in items)
        {
            if (lookup.TryGetValue(item.PlayerId, out var player))
            {
                item.Player = player;
            }
        }

        return items;
    }

    public async Task<IReadOnlyList<TransferMarketItemDto>> GetOpenItemsAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var limit = Math.Max(1, _options.QuantidadePorRodada);

        var items = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(x => x.Status == "OPEN")
            .OrderByDescending(x => x.DataInicioUtc)
            .Take(limit)
            .Select(x => new TransferMarketItemDto(
                x.MarketItemId,
                x.PlayerId,
                x.Player.Name,
                x.Player.Position.Name,
                x.Player.Age ?? 0,
                x.Player.Overall,
                x.PrecoBase,
                x.PrecoComprarAgora,
                x.LanceAtual,
                x.MaiorLanceTeam != null ? x.MaiorLanceTeam.TeamName : string.Empty,
                x.Status,
                x.DataInicioUtc))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return items;
    }

    private async Task SelectPlayersForRangeAsync(
        int minOverall,
        int maxOverall,
        int quantity,
        HashSet<int> excludedPlayerIds,
        List<Player> selectedPlayers,
        CancellationToken ct)
    {
        if (quantity <= 0)
        {
            return;
        }

        var candidates = await _dbContext.Players
            .AsNoTracking()
            .Include(p => p.Position)
            .Where(p => p.Age.HasValue)
            .Where(p => p.Overall >= minOverall && p.Overall <= maxOverall)
            .Where(p => !p.TeamRosters.Any())
            .Where(p => !excludedPlayerIds.Contains(p.PlayerId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (candidates.Count < quantity)
        {
            throw new MarketGenerationValidationException(
                $"Jogadores insuficientes na faixa {minOverall}-{maxOverall} (disponíveis: {candidates.Count}, necessários: {quantity}).");
        }

        var picked = TakeRandomDistinct(candidates, quantity);
        foreach (var player in picked)
        {
            excludedPlayerIds.Add(player.PlayerId);
            selectedPlayers.Add(player);
        }
    }

    private async Task SelectRarePlayersAsync(
        int minOverall,
        int maxOverall,
        int quantity,
        HashSet<int> excludedPlayerIds,
        List<Player> selectedPlayers,
        CancellationToken ct)
    {
        if (quantity <= 0)
        {
            return;
        }

        var candidates = await _dbContext.Players
            .AsNoTracking()
            .Include(p => p.Position)
            .Where(p => p.Age.HasValue)
            .Where(p => p.Overall >= minOverall && p.Overall <= maxOverall)
            .Where(p => !p.TeamRosters.Any())
            .Where(p => !excludedPlayerIds.Contains(p.PlayerId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (candidates.Count < quantity)
        {
            throw new MarketGenerationValidationException(
                $"Jogadores insuficientes na faixa rara {minOverall}-{maxOverall} (disponíveis: {candidates.Count}, necessários: {quantity}).");
        }

        var working = new List<Player>(candidates);
        for (var i = 0; i < quantity; i++)
        {
            var player = PickWeightedRarePlayer(working);
            selectedPlayers.Add(player);
            excludedPlayerIds.Add(player.PlayerId);
            working.Remove(player);
        }
    }

    private static List<Player> TakeRandomDistinct(List<Player> candidates, int quantity)
    {
        var pool = new List<Player>(candidates);
        var result = new List<Player>(quantity);

        for (var i = 0; i < quantity; i++)
        {
            var index = RandomNumberGenerator.GetInt32(pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    private static Player PickWeightedRarePlayer(IReadOnlyList<Player> candidates)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // Peso decrescente conforme o overall aumenta: peso = 1 / (ovr - 76)
        var weights = new double[candidates.Count];
        double totalWeight = 0d;
        for (var i = 0; i < candidates.Count; i++)
        {
            var ovr = candidates[i].Overall;
            var weight = 1d / (ovr - 76);
            weights[i] = weight;
            totalWeight += weight;
        }

        var random = RandomNumberGenerator.GetInt32(int.MaxValue);
        var normalized = random / (double)int.MaxValue;
        var threshold = normalized * totalWeight;

        double cumulative = 0d;
        for (var i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (threshold <= cumulative)
            {
                return candidates[i];
            }
        }

        return candidates[^1];
    }
}
