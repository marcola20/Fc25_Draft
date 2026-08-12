using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class HallOfFameService : IHallOfFameService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public HallOfFameService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<HallOfFameEntryDto>> GetAllAsync(CancellationToken ct)
    {
        var list = await _db.HallOfFame
            .AsNoTracking()
            .OrderByDescending(x => x.Ano)
            .ThenByDescending(x => x.CriadoEm)
            .ToListAsync(ct);
        return list.Select(ToDto).ToArray();
    }

    public async Task<HallOfFameEntryDto> CreateAsync(HallOfFameCreateRequest request, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var entry = new HallOfFameEntry
        {
            HallOfFameId = Guid.NewGuid(),
            Descricao = NormalizarDescricao(request.Descricao),
            Tipo = request.Tipo,
            TimeCampeao = NormalizarTime(request.TimeCampeao),
            Ano = request.Ano,
            Temporada = NormalizarTemporada(request.Temporada),
            CriadoEm = now,
            AtualizadoEm = now
        };

        _db.HallOfFame.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<HallOfFameEntryDto> UpdateAsync(Guid id, HallOfFameUpdateRequest request, CancellationToken ct)
    {
        var entry = await _db.HallOfFame.FirstOrDefaultAsync(x => x.HallOfFameId == id, ct)
            ?? throw new InvalidOperationException("Entrada do Hall of Fame não encontrada.");

        entry.Descricao = NormalizarDescricao(request.Descricao);
        entry.Tipo = request.Tipo;
        entry.TimeCampeao = NormalizarTime(request.TimeCampeao);
        entry.Ano = request.Ano;
        entry.Temporada = NormalizarTemporada(request.Temporada);
        entry.AtualizadoEm = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entry = await _db.HallOfFame.FirstOrDefaultAsync(x => x.HallOfFameId == id, ct)
            ?? throw new InvalidOperationException("Entrada do Hall of Fame não encontrada.");

        _db.HallOfFame.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    private static string NormalizarDescricao(string descricao)
    {
        var valor = descricao?.Trim();
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException("Informe a descrição.");
        return valor;
    }

    private static string NormalizarTime(string time)
    {
        var valor = time?.Trim();
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException("Informe o time campeão.");
        return valor;
    }

    private static string? NormalizarTemporada(string? temporada)
    {
        var valor = temporada?.Trim();
        return string.IsNullOrWhiteSpace(valor) ? null : valor;
    }

    private static HallOfFameEntryDto ToDto(HallOfFameEntry e) =>
        new(e.HallOfFameId, e.Descricao, e.Tipo, e.TimeCampeao, e.Ano, e.Temporada, e.CriadoEm, e.AtualizadoEm);
}
