using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Textos de anúncio das novidades e a explicação de cada regra, editados na tela /admin/anuncios.</summary>
public class AnuncioService : IAnuncioService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public AnuncioService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<AnuncioDto>> ListAsync(CancellationToken ct)
    {
        var anuncios = await _db.Anuncios.AsNoTracking().ToListAsync(ct);

        // Grupo com algo ainda por mandar vem primeiro; depois o grupo mais novo.
        return anuncios
            .GroupBy(a => a.Grupo)
            .OrderByDescending(g => g.Any(a => a.EnviadoEm is null))
            .ThenByDescending(g => g.Max(a => a.CriadoEm))
            .SelectMany(g => g.OrderBy(a => a.Ordem).ThenBy(a => a.CriadoEm))
            .Select(ToDto)
            .ToArray();
    }

    public async Task<AnuncioDto> CriarAsync(AnuncioSalvarRequest request, CancellationToken ct)
    {
        Validar(request);

        var agora = _time.GetUtcNow().UtcDateTime;
        var anuncio = new Anuncio
        {
            AnuncioId = Guid.NewGuid(),
            CriadoEm = agora
        };
        Aplicar(anuncio, request, agora);

        _db.Anuncios.Add(anuncio);
        await _db.SaveChangesAsync(ct);

        return ToDto(anuncio);
    }

    public async Task<AnuncioDto> SalvarAsync(Guid anuncioId, AnuncioSalvarRequest request, CancellationToken ct)
    {
        Validar(request);

        var anuncio = await BuscarAsync(anuncioId, ct);
        Aplicar(anuncio, request, _time.GetUtcNow().UtcDateTime);
        await _db.SaveChangesAsync(ct);

        return ToDto(anuncio);
    }

    public async Task<AnuncioDto> MarcarEnviadoAsync(Guid anuncioId, bool enviado, CancellationToken ct)
    {
        var anuncio = await BuscarAsync(anuncioId, ct);
        anuncio.EnviadoEm = enviado ? _time.GetUtcNow().UtcDateTime : null;
        await _db.SaveChangesAsync(ct);

        return ToDto(anuncio);
    }

    public async Task ExcluirAsync(Guid anuncioId, CancellationToken ct)
    {
        var anuncio = await BuscarAsync(anuncioId, ct);
        _db.Anuncios.Remove(anuncio);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Anuncio> BuscarAsync(Guid anuncioId, CancellationToken ct) =>
        await _db.Anuncios.FirstOrDefaultAsync(a => a.AnuncioId == anuncioId, ct)
            ?? throw new InvalidOperationException("Anúncio não encontrado.");

    private static void Validar(AnuncioSalvarRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo)) throw new ArgumentException("Informe um título.");
        if (string.IsNullOrWhiteSpace(request.Grupo)) throw new ArgumentException("Informe o grupo (ex.: Virada 2012).");
        if (request.Titulo.Trim().Length > 160) throw new ArgumentException("Título muito longo (máximo 160 letras).");
        if (request.Grupo.Trim().Length > 80) throw new ArgumentException("Grupo muito longo (máximo 80 letras).");
    }

    private static void Aplicar(Anuncio anuncio, AnuncioSalvarRequest request, DateTime agora)
    {
        anuncio.Grupo = request.Grupo.Trim();
        anuncio.Titulo = request.Titulo.Trim();
        anuncio.Texto = (request.Texto ?? "").TrimEnd();
        anuncio.Explicacao = (request.Explicacao ?? "").TrimEnd();
        anuncio.Ordem = request.Ordem;
        anuncio.AtualizadoEm = agora;
    }

    private static AnuncioDto ToDto(Anuncio a) =>
        new(a.AnuncioId, a.Grupo, a.Titulo, a.Texto, a.Explicacao, a.Ordem, a.EnviadoEm, a.AtualizadoEm);
}
