using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class MarketItemPublicationService : IMarketItemPublicationService
{
    private readonly DraftDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public MarketItemPublicationService(DraftDbContext dbContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<MarketItemPublicationDto>> ListAsync(CancellationToken ct)
    {
        var items = await _dbContext.MarketItems
            .AsNoTracking()
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return items.Select(MapToDto).ToList();
    }

    public async Task<MarketItemPublicationDto?> GetAsync(Guid itemId, CancellationToken ct)
    {
        var item = await _dbContext.MarketItems
            .AsNoTracking()
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
            .ConfigureAwait(false);

        return item is null ? null : MapToDto(item);
    }

    public async Task<MarketItemPublicationDto> CreateDraftAsync(MarketItemDraftCreateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var errors = ValidateDraftInput(request.BasePrice, request.BuyNowPrice, request.MinIncrement, request.ExpiresAtUtc, now);
        if (request.CycleId == Guid.Empty)
        {
            AddError(errors, "cycleId", "O ciclo do mercado é obrigatório.");
        }

        if (request.PlayerId <= 0)
        {
            AddError(errors, "playerId", "O jogador é obrigatório.");
        }

        if (errors.Count > 0)
        {
            throw new MarketItemValidationException("Não foi possível criar o item de mercado.", BuildErrorDictionary(errors));
        }

        var cycle = await _dbContext.MarketCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CycleId == request.CycleId, ct)
            .ConfigureAwait(false);

        if (cycle is null)
        {
            throw new MarketItemValidationException(
                "Não foi possível criar o item de mercado.",
                BuildErrorDictionary(new Dictionary<string, List<string>>
                {
                    ["cycleId"] = new() { "O ciclo informado não foi encontrado." }
                }));
        }

        if (cycle.Status != MarketCycleStatus.Active)
        {
            throw new MarketConflictException("O ciclo informado não está ativo.");
        }

        var player = await _dbContext.Players
            .Include(p => p.Position)
            .FirstOrDefaultAsync(p => p.PlayerId == request.PlayerId, ct)
            .ConfigureAwait(false);

        if (player is null)
        {
            throw new MarketItemValidationException(
                "Não foi possível criar o item de mercado.",
                BuildErrorDictionary(new Dictionary<string, List<string>>
                {
                    ["playerId"] = new() { "O jogador informado não foi encontrado." }
                }));
        }

        var exists = await _dbContext.MarketItems
            .AsNoTracking()
            .AnyAsync(i => i.CycleId == request.CycleId && i.PlayerId == request.PlayerId, ct)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new MarketConflictException("Já existe um item cadastrado para este jogador neste ciclo.");
        }

        var entity = new MarketItem
        {
            ItemId = Guid.NewGuid(),
            CycleId = request.CycleId,
            PlayerId = request.PlayerId,
            BasePrice = request.BasePrice,
            BuyNowPrice = request.BuyNowPrice,
            MinIncrement = request.MinIncrement,
            ExpiresAtUtc = NormalizeToUtc(request.ExpiresAtUtc),
            Status = MarketItemStatus.Draft,
            CreatedAtUtc = now,
            LastUpdateUtc = now
        };

        await _dbContext.MarketItems.AddAsync(entity, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        entity.Player = player;

        return MapToDto(entity);
    }

    public async Task<MarketItemPublicationDto> UpdateDraftAsync(
        Guid itemId,
        MarketItemDraftUpdateRequest request,
        uint expectedRowVersion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var errors = ValidateDraftInput(request.BasePrice, request.BuyNowPrice, request.MinIncrement, request.ExpiresAtUtc, now);
        if (errors.Count > 0)
        {
            throw new MarketItemValidationException("Não foi possível atualizar o item de mercado.", BuildErrorDictionary(errors));
        }

        var item = await _dbContext.MarketItems
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
            .ConfigureAwait(false)
            ?? throw new MarketNotFoundException("Item de mercado não encontrado.");

        if (item.Status != MarketItemStatus.Draft)
        {
            throw new MarketConflictException("Somente itens em rascunho podem ser editados.");
        }

        EnsureRowVersion(item.RowVersion, expectedRowVersion);

        item.BasePrice = request.BasePrice;
        item.BuyNowPrice = request.BuyNowPrice;
        item.MinIncrement = request.MinIncrement;
        item.ExpiresAtUtc = NormalizeToUtc(request.ExpiresAtUtc);
        item.LastUpdateUtc = now;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return MapToDto(item);
    }

    public async Task<MarketItemPublicationDto> PublishAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct)
    {
        var item = await _dbContext.MarketItems
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
            .ConfigureAwait(false)
            ?? throw new MarketNotFoundException("Item de mercado não encontrado.");

        if (item.Status != MarketItemStatus.Draft)
        {
            throw new MarketConflictException("Somente itens em rascunho podem ser publicados.");
        }

        EnsureRowVersion(item.RowVersion, expectedRowVersion);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (item.ExpiresAtUtc <= now)
        {
            throw new MarketConflictException("A data de expiração deve ser futura para publicar o item.");
        }

        item.Status = MarketItemStatus.Published;
        item.PublishedAtUtc = now;
        item.LastUpdateUtc = now;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return MapToDto(item);
    }

    public async Task<MarketItemPublicationDto> SoftDeleteAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct)
    {
        var item = await _dbContext.MarketItems
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
            .ConfigureAwait(false)
            ?? throw new MarketNotFoundException("Item de mercado não encontrado.");

        if (item.Status != MarketItemStatus.Draft)
        {
            throw new MarketConflictException("Somente itens em rascunho podem ser removidos.");
        }

        EnsureRowVersion(item.RowVersion, expectedRowVersion);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        item.Status = MarketItemStatus.Canceled;
        item.LastUpdateUtc = now;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return MapToDto(item);
    }

    private static MarketItemPublicationDto MapToDto(MarketItem item)
    {
        var player = item.Player ?? throw new InvalidOperationException("O jogador associado ao item não foi carregado.");
        var positionName = player.Position?.Name ?? string.Empty;

        return new MarketItemPublicationDto(
            item.ItemId,
            item.CycleId,
            item.PlayerId,
            player.Name,
            positionName,
            player.Overall,
            player.Age,
            item.BasePrice,
            item.BuyNowPrice,
            item.MinIncrement,
            item.ExpiresAtUtc,
            item.Status.ToString(),
            item.CreatedAtUtc,
            item.PublishedAtUtc,
            item.LastUpdateUtc,
            item.RowVersion);
    }

    private static Dictionary<string, List<string>> ValidateDraftInput(
        decimal basePrice,
        decimal? buyNowPrice,
        decimal minIncrement,
        DateTime expiresAtUtc,
        DateTime currentUtc)
    {
        var errors = new Dictionary<string, List<string>>();

        if (basePrice <= 0m)
        {
            AddError(errors, "basePrice", "O valor base deve ser maior que zero.");
        }

        if (minIncrement <= 0m)
        {
            AddError(errors, "minIncrement", "O incremento mínimo deve ser maior que zero.");
        }

        if (buyNowPrice.HasValue && buyNowPrice.Value <= basePrice)
        {
            AddError(errors, "buyNowPrice", "O valor de compra imediata deve ser maior que o valor base.");
        }

        if (NormalizeToUtc(expiresAtUtc) <= currentUtc)
        {
            AddError(errors, "expiresAtUtc", "A data de expiração deve ser futura.");
        }

        return errors;
    }

    private static void AddError(IDictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = new List<string>();
            errors[key] = list;
        }

        list.Add(message);
    }

    private static IReadOnlyDictionary<string, string[]> BuildErrorDictionary(Dictionary<string, List<string>> errors)
    {
        return errors.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.ToArray());
    }

    private static void EnsureRowVersion(uint current, uint expected)
    {
        if (current != expected)
        {
            throw new MarketPreconditionFailedException("A versão informada do recurso está desatualizada.");
        }
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
