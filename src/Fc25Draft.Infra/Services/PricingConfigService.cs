using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class PricingConfigService : IPricingConfigService
{
    private readonly DraftDbContext _db;

    public PricingConfigService(DraftDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PricingConfigDto> GetAsync(CancellationToken ct)
    {
        var cfg = await _db.PricingConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = PricingConfig.Default();
            cfg.AtualizadoEm = DateTime.UtcNow;
            _db.PricingConfigs.Add(cfg);
            await _db.SaveChangesAsync(ct);
        }

        return ToDto(cfg);
    }

    public async Task<PricingConfigDto> UpdateAsync(PricingConfigDto dto, CancellationToken ct)
    {
        Validate(dto);

        var cfg = await _db.PricingConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new PricingConfig { Id = 1 };
            _db.PricingConfigs.Add(cfg);
        }

        cfg.BaseScale = dto.BaseScale;
        cfg.OverallBase = dto.OverallBase;
        cfg.OverallPivot = dto.OverallPivot;
        cfg.BuyNowFactor = dto.BuyNowFactor;
        cfg.BuyNowFactorEspecial = dto.BuyNowFactorEspecial;
        cfg.MinIncrementRate = dto.MinIncrementRate;
        cfg.MinIncrementStep = dto.MinIncrementStep;
        cfg.AgeFactorUpTo22 = dto.AgeFactorUpTo22;
        cfg.AgeFactor23To24 = dto.AgeFactor23To24;
        cfg.AgeFactor25To26 = dto.AgeFactor25To26;
        cfg.AgeFactor27To28 = dto.AgeFactor27To28;
        cfg.AgeFactor29To30 = dto.AgeFactor29To30;
        cfg.AgeFactor31To32 = dto.AgeFactor31To32;
        cfg.AgeFactor33To34 = dto.AgeFactor33To34;
        cfg.AgeFactor35Plus = dto.AgeFactor35Plus;
        cfg.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(cfg);
    }

    private static void Validate(PricingConfigDto d)
    {
        if (d.BaseScale <= 0) throw new InvalidOperationException("BaseScale deve ser maior que zero.");
        if (d.OverallBase <= 0) throw new InvalidOperationException("A base de overall deve ser maior que zero.");
        if (d.OverallPivot <= 0) throw new InvalidOperationException("O pivô de overall deve ser maior que zero.");
        if (d.BuyNowFactor <= 0) throw new InvalidOperationException("O fator do comprar-agora deve ser maior que zero.");
        if (d.BuyNowFactorEspecial <= 0) throw new InvalidOperationException("O fator do comprar-agora do ciclo especial deve ser maior que zero.");
        if (d.MinIncrementRate <= 0) throw new InvalidOperationException("A taxa de incremento mínimo deve ser maior que zero.");
        if (d.MinIncrementStep <= 0) throw new InvalidOperationException("O passo de arredondamento deve ser maior que zero.");

        var fatores = new[]
        {
            d.AgeFactorUpTo22, d.AgeFactor23To24, d.AgeFactor25To26, d.AgeFactor27To28,
            d.AgeFactor29To30, d.AgeFactor31To32, d.AgeFactor33To34, d.AgeFactor35Plus
        };
        if (fatores.Any(f => f <= 0))
            throw new InvalidOperationException("Todos os fatores de idade devem ser maiores que zero.");
    }

    private static PricingConfigDto ToDto(PricingConfig c) => new(
        c.BaseScale, c.OverallBase, c.OverallPivot, c.BuyNowFactor, c.MinIncrementRate, c.MinIncrementStep,
        c.AgeFactorUpTo22, c.AgeFactor23To24, c.AgeFactor25To26, c.AgeFactor27To28,
        c.AgeFactor29To30, c.AgeFactor31To32, c.AgeFactor33To34, c.AgeFactor35Plus,
        c.BuyNowFactorEspecial);

    public async Task<(int Atualizados, int Pulados)> ReaplicarComprarJaEspecialAsync(CancellationToken ct)
    {
        var cfg = await _db.PricingConfigs.AsNoTracking().FirstOrDefaultAsync(ct) ?? PricingConfig.Default();

        var itens = await _db.MarketItems
            .Where(i => i.Cycle.Theme != null
                && (i.Cycle.Status == MarketCycleStatus.Draft || i.Cycle.Status == MarketCycleStatus.Active)
                && (i.Status == MarketItemStatus.Draft || i.Status == MarketItemStatus.Active))
            .ToListAsync(ct);

        var agora = DateTime.UtcNow;
        int atualizados = 0, pulados = 0;
        foreach (var item in itens)
        {
            var novo = ComprarJaEspecial(item.BasePrice, cfg);
            if (item.CurrentLeaderAmount.HasValue && item.CurrentLeaderAmount.Value >= novo)
            {
                pulados++;
                continue;
            }

            if (item.BuyNowPrice == novo) continue;
            item.BuyNowPrice = novo;
            item.LastUpdateUtc = agora;
            atualizados++;
        }

        await _db.SaveChangesAsync(ct);
        return (atualizados, pulados);
    }

    /// <summary>Base × fator especial, arredondado ao passo (igual ao comprar-agora normal).</summary>
    public static decimal ComprarJaEspecial(decimal basePrice, PricingConfig cfg)
    {
        var step = cfg.MinIncrementStep <= 0 ? 1m : cfg.MinIncrementStep;
        var valor = basePrice * cfg.BuyNowFactorEspecial;
        if (valor <= 0) return step;
        var passos = Math.Round(decimal.Divide(valor, step), MidpointRounding.AwayFromZero);
        return (passos == 0 ? 1 : passos) * step;
    }
}
