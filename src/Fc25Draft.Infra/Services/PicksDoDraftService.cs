using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Picks do próximo draft (1ª e 2ª rodada de cada time da temporada). Negociáveis nas propostas até a loteria desse
/// draft acontecer; quando o draft é gerado, cada pick vai para o dono.
/// </summary>
public class PicksDoDraftService : IPicksDoDraftService
{
    public static readonly int[] Rodadas = [1, 2];

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public PicksDoDraftService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<PicksDoProximoDraftDto> ProximoDraftAsync(CancellationToken ct)
    {
        var (temporada, aberta) = await SituacaoAsync(_db, HorarioDeBrasilia.Agora(_time), ct);
        if (temporada is not int t) return new PicksDoProximoDraftDto(null, false, []);
        return new PicksDoProximoDraftDto(t, aberta, await PicksAsync(_db, t, ct));
    }

    public async Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct)
    {
        var negocios = await _db.TransferOffers.AsNoTracking()
            .Where(o => o.Status == OfferStatus.Accepted && o.Picks.Any())
            .Select(o => new
            {
                o.OfferId, o.UpdatedAtUtc, De = o.FromTeam.TeamName, Para = o.ToTeam.TeamName,
                Picks = o.Picks.Select(p => new { p.Rodada, Original = p.TimeOriginal.TeamName, p.IsTarget }).ToList(),
                Jogadores = o.Players.Count
            })
            .ToListAsync(ct);

        return negocios.Select(n =>
        {
            // Quem recebe cada pick: as pedidas (IsTarget) vão para quem propôs.
            var paraQuemPropos = n.Picks.Where(p => p.IsTarget).Select(p => $"{p.Rodada}ª rodada do {p.Original}").ToList();
            var paraOOutro = n.Picks.Where(p => !p.IsTarget).Select(p => $"{p.Rodada}ª rodada do {p.Original}").ToList();
            var titulo = paraQuemPropos.Count > 0
                ? $"{n.De} fica com a pick da {string.Join(" e da ", paraQuemPropos)} no negócio com o {n.Para}"
                : $"{n.Para} fica com a pick da {string.Join(" e da ", paraOOutro)} no negócio com o {n.De}";
            var linha = paraQuemPropos.Count > 0 && paraOOutro.Count > 0
                ? $"Na volta, o {n.Para} leva a da {string.Join(" e da ", paraOOutro)}"
                : n.Jogadores > 0 ? "Negócio com jogadores envolvidos" : "Negócio só de picks do próximo draft";
            return new PlantaoNoticiaDto(n.UpdatedAtUtc, PlantaoCategoria.Mercado, "🔄", titulo, linha, "/picks", n.De, null);
        }).ToList();
    }

    // ── Para as propostas e o draft ────────────────────────────────────────────

    /// <summary>
    /// Temporada do próximo draft (a em andamento: o draft sai da classificação dela) e se as picks ainda se negociam
    /// (fecha quando a loteria desse draft termina).
    /// </summary>
    public static async Task<(int? Temporada, bool Aberta)> SituacaoAsync(DraftDbContext db, DateTime agoraBrasilia, CancellationToken ct)
    {
        var serieA = await db.Ligas.AsNoTracking()
            .Where(l => l.Tipo == TipoCompetition.Liga && l.DataInicio <= agoraBrasilia
                        && (l.Divisao == null || l.Divisao == Divisao.SerieA))
            .OrderByDescending(l => l.DataInicio)
            .Select(l => new { l.LigaId, l.Temporada })
            .FirstOrDefaultAsync(ct);
        if (serieA?.Temporada is not int temporada) return (null, false);

        var loteriaFeita = await db.LigaLoterias.AsNoTracking().AnyAsync(l => l.LigaId == serieA.LigaId && l.IsFinished, ct);
        return (temporada, !loteriaFeita);
    }

    /// <summary>Todas as picks do draft da temporada, com o dono de cada uma.</summary>
    public static async Task<IReadOnlyList<PickDoDraftDto>> PicksAsync(DraftDbContext db, int temporada, CancellationToken ct)
    {
        var times = await db.LigaTimes.AsNoTracking()
            .Where(t => t.Liga.Temporada == temporada && t.Liga.Tipo == TipoCompetition.Liga)
            .Select(t => new { t.TimeId, t.Time.TeamName })
            .Distinct()
            .ToListAsync(ct);
        var nomes = times.ToDictionary(t => t.TimeId, t => t.TeamName);
        var donos = await DonosAsync(db, temporada, ct);

        return times
            .SelectMany(t => Rodadas.Select(r =>
            {
                var dono = donos.GetValueOrDefault((r, t.TimeId), t.TimeId);
                return new PickDoDraftDto(temporada, r, t.TimeId, t.TeamName, dono, nomes.GetValueOrDefault(dono, "?"));
            }))
            .OrderBy(p => p.Rodada).ThenBy(p => p.TimeOriginalNome)
            .ToList();
    }

    /// <summary>Dono de cada pick negociada do draft da temporada: (rodada, time original) → dono.</summary>
    public static async Task<Dictionary<(int Rodada, Guid TimeOriginal), Guid>> DonosAsync(DraftDbContext db, int temporada, CancellationToken ct) =>
        (await db.PicksDoDraft.AsNoTracking()
            .Where(p => p.Temporada == temporada)
            .Select(p => new { p.Rodada, p.TimeOriginalId, p.DonoId })
            .ToListAsync(ct))
        .ToDictionary(p => (p.Rodada, p.TimeOriginalId), p => p.DonoId);

    /// <summary>
    /// Confere que as picks são do próximo draft, ainda se negociam e são todas de <paramref name="dono"/>. Devolve o
    /// texto do problema, ou nulo se está tudo certo.
    /// </summary>
    public static async Task<string?> ProblemaAsync(DraftDbContext db, DateTime agoraBrasilia, IEnumerable<PickRefDto> picks,
        Guid dono, string nomeDoDono, CancellationToken ct)
    {
        var lista = picks.ToList();
        if (lista.Count == 0) return null;

        var (temporada, aberta) = await SituacaoAsync(db, agoraBrasilia, ct);
        if (temporada is null || !aberta) return "As picks do draft não se negociam mais: a loteria já aconteceu.";

        var todas = (await PicksAsync(db, temporada.Value, ct)).ToDictionary(p => p.Ref);
        foreach (var p in lista)
        {
            if (!todas.TryGetValue(p, out var pick)) return "Essa pick não é do próximo draft.";
            if (pick.DonoId != dono) return $"A pick da {pick.Rotulo} não é do {nomeDoDono}.";
        }
        return null;
    }

    /// <summary>Passa as picks para <paramref name="novoDono"/> (não salva: entra no SaveChanges da proposta).</summary>
    public static async Task TransferirAsync(DraftDbContext db, IEnumerable<PickRefDto> picks, Guid novoDono, DateTime agora, CancellationToken ct)
    {
        foreach (var p in picks)
        {
            var registro = await db.PicksDoDraft.FirstOrDefaultAsync(
                x => x.Temporada == p.Temporada && x.Rodada == p.Rodada && x.TimeOriginalId == p.TimeOriginalId, ct);
            if (registro is null)
            {
                db.PicksDoDraft.Add(new PickDoDraft
                {
                    PickDoDraftId = Guid.NewGuid(), Temporada = p.Temporada, Rodada = p.Rodada,
                    TimeOriginalId = p.TimeOriginalId, DonoId = novoDono, NegociadaEm = agora
                });
            }
            else
            {
                registro.DonoId = novoDono;
                registro.NegociadaEm = agora;
            }
        }
    }
}
