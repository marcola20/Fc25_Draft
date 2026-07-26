using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class TransferConfigService : ITransferConfigService
{
    private readonly DraftDbContext _db;

    public TransferConfigService(DraftDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<TransferConfigDto> GetAsync(CancellationToken ct)
    {
        var cfg = await _db.TransferConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = TransferConfig.Default();
            cfg.AtualizadoEm = DateTime.UtcNow;
            _db.TransferConfigs.Add(cfg);
            await _db.SaveChangesAsync(ct);
        }

        return ToDto(cfg);
    }

    public async Task<TransferConfigDto> UpdateAsync(TransferConfigDto dto, CancellationToken ct)
    {
        Validate(dto);

        var cfg = await _db.TransferConfigs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            cfg = new TransferConfig { Id = 1 };
            _db.TransferConfigs.Add(cfg);
        }

        cfg.MaxQuickSellPerWindow = dto.MaxQuickSellPerWindow;
        cfg.MaxTransfers = dto.MaxTransfers;
        cfg.MinRosterSize = dto.MinRosterSize;
        cfg.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(cfg);
    }

    private static void Validate(TransferConfigDto d)
    {
        if (d.MaxQuickSellPerWindow < 0) throw new InvalidOperationException("O limite de vendas rápidas não pode ser negativo.");
        if (d.MaxTransfers < 0) throw new InvalidOperationException("O limite de transferências não pode ser negativo.");
        if (d.MinRosterSize < 0) throw new InvalidOperationException("O mínimo de jogadores não pode ser negativo.");
    }

    private static TransferConfigDto ToDto(TransferConfig c) =>
        new(c.MaxQuickSellPerWindow, c.MaxTransfers, c.MinRosterSize);
}
