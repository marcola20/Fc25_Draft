using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Configurations;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>Mosaicos da rodada, cadastrados em /admin/mosaicos e assistidos em /telao/mosaico/{id}.</summary>
public class MosaicoService : IMosaicoService
{
    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public MosaicoService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<MosaicoDto>> ListarAsync(CancellationToken ct)
    {
        var mosaicos = await _db.Mosaicos.AsNoTracking()
            .OrderByDescending(m => m.CriadoEm)
            .ToListAsync(ct);
        var rodadas = await RodadasAsync(mosaicos.Select(m => m.MosaicoId).ToList(), ct);

        return mosaicos.Select(m => ToDto(m, rodadas)).ToArray();
    }

    public async Task<IReadOnlyList<MosaicoCompeticaoOpcaoDto>> OpcoesAsync(CancellationToken ct)
    {
        var temporada = await _db.Ligas.AsNoTracking().MaxAsync(l => l.Temporada, ct);

        var ligas = await _db.Ligas.AsNoTracking()
            .Where(l => l.Temporada == temporada)
            .Select(l => new
            {
                l.LigaId,
                l.Tipo,
                l.Divisao,
                l.Temporada,
                Rodadas = l.Rodadas
                    .Where(r => r.Partidas.Any())
                    .Select(r => new { r.RodadaId, r.Numero, r.Desempate, r.DataHora })
                    .ToList()
            })
            .ToListAsync(ct);

        return ligas
            .OrderBy(l => l.Tipo).ThenBy(l => l.Divisao)
            .Select(l =>
            {
                var rodadas = l.Rodadas.OrderBy(r => r.Desempate).ThenBy(r => r.Numero).ToList();
                // Nos pontos corridos já vem marcada a última rodada, que é a do mosaico.
                var sugerida = l.Tipo == TipoCompetition.Liga
                    ? rodadas.Where(r => !r.Desempate && r.Numero > 0).OrderByDescending(r => r.Numero).Select(r => (Guid?)r.RodadaId).FirstOrDefault()
                    : null;
                return new MosaicoCompeticaoOpcaoDto(
                    l.LigaId, LigaLabels.Competicao(l.Tipo, l.Divisao), l.Temporada,
                    rodadas.Select(r => new MosaicoRodadaOpcaoDto(r.RodadaId, r.Desempate ? 0 : r.Numero, r.DataHora)).ToArray(),
                    sugerida);
            })
            .Where(l => l.Rodadas.Count > 0)
            .ToArray();
    }

    public async Task<MosaicoDto> CriarAsync(MosaicoSalvarRequest request, CancellationToken ct)
    {
        var (titulo, videoId, rodadaIds) = await ValidarAsync(request, ct);

        var mosaico = new Mosaico
        {
            MosaicoId = Guid.NewGuid(),
            Titulo = titulo,
            YoutubeVideoId = videoId,
            CriadoEm = _time.GetUtcNow().UtcDateTime,
            Rodadas = rodadaIds.Select(id => new MosaicoRodada { RodadaId = id }).ToList()
        };

        _db.Mosaicos.Add(mosaico);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(mosaico.MosaicoId, ct);
    }

    public async Task<MosaicoDto> AtualizarAsync(Guid mosaicoId, MosaicoSalvarRequest request, CancellationToken ct)
    {
        var mosaico = await _db.Mosaicos.Include(m => m.Rodadas).FirstOrDefaultAsync(m => m.MosaicoId == mosaicoId, ct)
            ?? throw new InvalidOperationException("Mosaico não encontrado.");

        var (titulo, videoId, rodadaIds) = await ValidarAsync(request, ct);
        mosaico.Titulo = titulo;
        mosaico.YoutubeVideoId = videoId;

        var saem = mosaico.Rodadas.Where(r => !rodadaIds.Contains(r.RodadaId)).ToList();
        foreach (var r in saem) mosaico.Rodadas.Remove(r);
        foreach (var id in rodadaIds.Where(id => mosaico.Rodadas.All(r => r.RodadaId != id)))
            mosaico.Rodadas.Add(new MosaicoRodada { MosaicoId = mosaicoId, RodadaId = id });

        await _db.SaveChangesAsync(ct);
        return await GetAsync(mosaicoId, ct);
    }

    public async Task ApagarAsync(Guid mosaicoId, CancellationToken ct)
    {
        // Chat, reações e rodadas saem junto (cascata no banco).
        await _db.Mosaicos.Where(m => m.MosaicoId == mosaicoId).ExecuteDeleteAsync(ct);
    }

    public async Task<TelaoMosaicoDto?> TelaoAsync(Guid mosaicoId, CancellationToken ct)
    {
        var mosaico = await _db.Mosaicos.AsNoTracking().FirstOrDefaultAsync(m => m.MosaicoId == mosaicoId, ct);
        if (mosaico is null) return null;

        var dto = ToDto(mosaico, await RodadasAsync(new List<Guid> { mosaicoId }, ct));
        var rodadaIds = dto.Rodadas.Select(r => r.RodadaId).ToList();
        var ordem = dto.Rodadas.Select((r, i) => (r.RodadaId, i)).ToDictionary(x => x.RodadaId, x => x.i);
        var competicao = dto.Rodadas.ToDictionary(r => r.RodadaId, r => r.Competicao);

        var partidas = await _db.LigaPartidas.AsNoTracking()
            .Where(p => rodadaIds.Contains(p.RodadaId))
            .Select(p => new
            {
                p.PartidaId,
                p.RodadaId,
                Casa = p.TimeCasa.TeamName,
                Fora = p.TimeFora.TeamName,
                p.Status,
                p.GolsCasa,
                p.GolsFora,
                p.TemPenaltis,
                Penaltis = p.PenaltisVencedor != null ? p.PenaltisVencedor.TeamName : null,
                p.DataHora
            })
            .ToListAsync(ct);

        var jogos = partidas
            .OrderBy(p => ordem[p.RodadaId]).ThenBy(p => p.DataHora ?? DateTime.MaxValue).ThenBy(p => p.Casa)
            .Select(p => new MosaicoJogoDto(
                p.PartidaId, competicao[p.RodadaId], p.Casa, p.Fora, p.Status, p.GolsCasa, p.GolsFora,
                p.TemPenaltis ? p.Penaltis : null))
            .ToArray();

        return new TelaoMosaicoDto(dto.MosaicoId, dto.Titulo, dto.YoutubeVideoId, dto.CriadoEm, dto.Rodadas, jogos);
    }

    public async Task<MosaicoDto?> NaCentralAsync(CancellationToken ct)
    {
        var desde = _time.GetUtcNow().UtcDateTime.AddDays(-IMosaicoService.DiasNaCentral);
        var mosaico = await _db.Mosaicos.AsNoTracking()
            .Where(m => m.YoutubeVideoId != null && m.CriadoEm >= desde)
            .OrderByDescending(m => m.CriadoEm)
            .FirstOrDefaultAsync(ct);
        if (mosaico is null) return null;

        return ToDto(mosaico, await RodadasAsync(new List<Guid> { mosaico.MosaicoId }, ct));
    }

    private async Task<MosaicoDto> GetAsync(Guid mosaicoId, CancellationToken ct)
    {
        var mosaico = await _db.Mosaicos.AsNoTracking().FirstAsync(m => m.MosaicoId == mosaicoId, ct);
        return ToDto(mosaico, await RodadasAsync(new List<Guid> { mosaicoId }, ct));
    }

    private async Task<(string Titulo, string? VideoId, HashSet<Guid> RodadaIds)> ValidarAsync(
        MosaicoSalvarRequest request, CancellationToken ct)
    {
        var titulo = (request.Titulo ?? "").Trim();
        if (titulo.Length == 0)
            throw new InvalidOperationException("Dê um título para o mosaico.");
        if (titulo.Length > MosaicoConfiguration.TamanhoTitulo)
            throw new InvalidOperationException($"O título passa de {MosaicoConfiguration.TamanhoTitulo} letras.");

        string? videoId = null;
        if (!string.IsNullOrWhiteSpace(request.Link))
            videoId = YoutubeLink.ExtrairId(request.Link)
                ?? throw new InvalidOperationException("Não reconheci esse link do YouTube.");

        var rodadaIds = (request.RodadaIds ?? Array.Empty<Guid>()).ToHashSet();
        if (rodadaIds.Count == 0)
            throw new InvalidOperationException("Escolha pelo menos uma rodada.");

        var existem = await _db.LigaRodadas.CountAsync(r => rodadaIds.Contains(r.RodadaId), ct);
        if (existem != rodadaIds.Count)
            throw new InvalidOperationException("Alguma rodada escolhida não existe mais. Recarregue a página.");

        return (titulo, videoId, rodadaIds);
    }

    /// <summary>As rodadas de cada mosaico, Série A antes da Série B e da Copa.</summary>
    private async Task<Dictionary<Guid, List<MosaicoRodadaDto>>> RodadasAsync(List<Guid> mosaicoIds, CancellationToken ct)
    {
        var linhas = await _db.MosaicoRodadas.AsNoTracking()
            .Where(x => mosaicoIds.Contains(x.MosaicoId))
            .Select(x => new
            {
                x.MosaicoId,
                x.RodadaId,
                x.Rodada.LigaId,
                x.Rodada.Numero,
                x.Rodada.Desempate,
                x.Rodada.Liga.Tipo,
                x.Rodada.Liga.Divisao
            })
            .ToListAsync(ct);

        return linhas
            .GroupBy(x => x.MosaicoId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.Tipo).ThenBy(x => x.Divisao).ThenBy(x => x.Numero)
                    .Select(x =>
                    {
                        var competicao = LigaLabels.Competicao(x.Tipo, x.Divisao);
                        var rodada = x.Desempate ? "Jogo decisivo" : $"Rodada {x.Numero}";
                        return new MosaicoRodadaDto(x.RodadaId, x.LigaId, competicao, x.Numero, $"{competicao} · {rodada}");
                    })
                    .ToList());
    }

    private static MosaicoDto ToDto(Mosaico m, Dictionary<Guid, List<MosaicoRodadaDto>> rodadas) =>
        new(m.MosaicoId, m.Titulo, m.YoutubeVideoId, m.CriadoEm,
            rodadas.GetValueOrDefault(m.MosaicoId) ?? new List<MosaicoRodadaDto>());
}
