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
        cfg.QuickSellBloqueado = dto.QuickSellBloqueado;
        cfg.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(cfg);
    }

    public async Task<IReadOnlyList<TeamQuickSellStatusDto>> GetQuickSellStatusAsync(CancellationToken ct)
    {
        return await _db.Teams
            .AsNoTracking()
            .OrderBy(t => t.TeamName)
            .Select(t => new TeamQuickSellStatusDto(t.TeamId, t.TeamName, t.OwnerName, t.QuickSellCount, t.QuickSellLimitOverride))
            .ToListAsync(ct);
    }

    public async Task<int> ResetQuickSellCountsAsync(CancellationToken ct)
    {
        return await _db.Teams
            .Where(t => t.QuickSellCount != 0)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.QuickSellCount, 0), ct);
    }

    public async Task SetQuickSellLimitAsync(Guid teamId, int? limite, CancellationToken ct)
    {
        if (limite < 0) throw new InvalidOperationException("O limite de vendas rápidas não pode ser negativo.");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
            ?? throw new InvalidOperationException("Time não encontrado.");

        team.QuickSellLimitOverride = limite;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetQuickSellCountAsync(Guid teamId, int usados, CancellationToken ct)
    {
        if (usados < 0) throw new InvalidOperationException("O contador de vendas rápidas não pode ser negativo.");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
            ?? throw new InvalidOperationException("Time não encontrado.");

        team.QuickSellCount = usados;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TeamElencoMinimoDto>> ListElencoMinimoAsync(CancellationToken ct)
    {
        return await _db.Teams
            .AsNoTracking()
            .OrderBy(t => t.TeamName)
            .Select(t => new TeamElencoMinimoDto(t.TeamId, t.TeamName, t.OwnerName, t.Roster.Count, t.MinRosterSizeOverride))
            .ToListAsync(ct);
    }

    public async Task SetElencoMinimoTemporarioAsync(Guid teamId, int? minimo, CancellationToken ct)
    {
        if (minimo < 0) throw new InvalidOperationException("O mínimo de jogadores não pode ser negativo.");

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
            ?? throw new InvalidOperationException("Time não encontrado.");

        team.MinRosterSizeOverride = minimo;
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(TransferConfigDto d)
    {
        if (d.MaxQuickSellPerWindow < 0) throw new InvalidOperationException("O limite de vendas rápidas não pode ser negativo.");
        if (d.MaxTransfers < 0) throw new InvalidOperationException("O limite de transferências não pode ser negativo.");
        if (d.MinRosterSize < 0) throw new InvalidOperationException("O mínimo de jogadores não pode ser negativo.");
    }

    private static TransferConfigDto ToDto(TransferConfig c) =>
        new(c.MaxQuickSellPerWindow, c.MaxTransfers, c.MinRosterSize, c.QuickSellBloqueado);
}
