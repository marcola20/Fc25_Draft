using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Regulamento por temporada: o admin cria o do ano novo copiando o anterior e ajusta o texto.</summary>
public class RegulamentoService : IRegulamentoService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public RegulamentoService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<RegulamentoDto>> ListAsync(CancellationToken ct)
    {
        var regulamentos = await _db.Regulamentos.AsNoTracking()
            .OrderByDescending(r => r.Temporada)
            .ToListAsync(ct);

        return regulamentos.Select(ToDto).ToArray();
    }

    public async Task<RegulamentoDto?> GetAsync(Guid regulamentoId, CancellationToken ct)
    {
        var regulamento = await _db.Regulamentos.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RegulamentoId == regulamentoId, ct);

        return regulamento is null ? null : ToDto(regulamento);
    }

    public async Task<RegulamentoDto?> GetPorTemporadaAsync(int temporada, CancellationToken ct)
    {
        var regulamento = await _db.Regulamentos.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Temporada == temporada, ct);

        return regulamento is null ? null : ToDto(regulamento);
    }

    public async Task<RegulamentoDto?> GetAtualAsync(CancellationToken ct)
    {
        var regulamento = await _db.Regulamentos.AsNoTracking()
            .OrderByDescending(r => r.Temporada)
            .FirstOrDefaultAsync(ct);

        return regulamento is null ? null : ToDto(regulamento);
    }

    public async Task<RegulamentoDto> CriarAsync(RegulamentoCriarRequest request, CancellationToken ct)
    {
        if (request.Temporada < 1900 || request.Temporada > 2999)
            throw new ArgumentException("Temporada inválida.");

        if (await _db.Regulamentos.AnyAsync(r => r.Temporada == request.Temporada, ct))
            throw new InvalidOperationException($"A temporada {request.Temporada} já tem regulamento cadastrado.");

        var conteudo = "";
        if (request.CopiarDe is Guid origemId)
        {
            var origem = await _db.Regulamentos.AsNoTracking().FirstOrDefaultAsync(r => r.RegulamentoId == origemId, ct)
                ?? throw new InvalidOperationException("Regulamento de origem não encontrado.");

            conteudo = origem.Conteudo;
        }

        var agora = _time.GetUtcNow().UtcDateTime;
        var regulamento = new Regulamento
        {
            RegulamentoId = Guid.NewGuid(),
            Temporada = request.Temporada,
            Titulo = string.IsNullOrWhiteSpace(request.Titulo) ? $"Regulamento {request.Temporada}" : request.Titulo.Trim(),
            Conteudo = conteudo,
            CriadoEm = agora,
            AtualizadoEm = agora
        };

        _db.Regulamentos.Add(regulamento);
        await _db.SaveChangesAsync(ct);

        return ToDto(regulamento);
    }

    public async Task<RegulamentoDto> SalvarAsync(Guid regulamentoId, string titulo, string conteudo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("Informe um título.");
        if (string.IsNullOrWhiteSpace(conteudo)) throw new ArgumentException("O regulamento não pode ficar vazio.");

        var regulamento = await _db.Regulamentos.FirstOrDefaultAsync(r => r.RegulamentoId == regulamentoId, ct)
            ?? throw new InvalidOperationException("Regulamento não encontrado.");

        regulamento.Titulo = titulo.Trim();
        regulamento.Conteudo = conteudo;
        regulamento.AtualizadoEm = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        return ToDto(regulamento);
    }

    public async Task ExcluirAsync(Guid regulamentoId, CancellationToken ct)
    {
        var regulamento = await _db.Regulamentos.FirstOrDefaultAsync(r => r.RegulamentoId == regulamentoId, ct)
            ?? throw new InvalidOperationException("Regulamento não encontrado.");

        _db.Regulamentos.Remove(regulamento);
        await _db.SaveChangesAsync(ct);
    }

    private static RegulamentoDto ToDto(Regulamento r) =>
        new(r.RegulamentoId, r.Temporada, r.Titulo, r.Conteudo, r.AtualizadoEm);
}
