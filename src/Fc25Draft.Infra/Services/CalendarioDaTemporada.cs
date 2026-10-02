using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Calendário de uma temporada: sai da abertura gravada (<see cref="AberturaTemporada"/>) e do
/// tamanho de cada competição (rodadas da Série A e da B = times - 1; grupos da Copa = rodadas criadas).
/// </summary>
internal static class CalendarioDaTemporada
{
    /// <summary>
    /// Temporada cujo calendário vale para a liga. A Supercopa de uma temporada (campeão da Série A
    /// x campeão da Copa) é o jogo que abre a seguinte.
    /// </summary>
    public static int? TemporadaDoCalendario(Liga liga) =>
        liga.Tipo == TipoCompetition.Supercopa ? liga.Temporada + 1 : liga.Temporada;

    public static async Task<DateTime?> AberturaAsync(DraftDbContext db, int temporada, CancellationToken ct) =>
        await db.AberturasTemporada.AsNoTracking()
            .Where(a => a.Temporada == temporada)
            .Select(a => (DateTime?)a.Abertura)
            .FirstOrDefaultAsync(ct);

    /// <summary>Calendário pela conta a partir da abertura; nulo se a temporada ainda não tem abertura.</summary>
    public static async Task<IReadOnlyList<DataDaTemporada>?> MontarAsync(DraftDbContext db, int temporada, CancellationToken ct)
    {
        if (await AberturaAsync(db, temporada, ct) is not DateTime abertura) return null;

        var rodadasSerieA = await RodadasDaDivisaoAsync(db, temporada, Divisao.SerieA, ct);
        var rodadasSerieB = await RodadasDaDivisaoAsync(db, temporada, Divisao.SerieB, ct);
        var rodadasGrupoCopa = await db.LigaRodadas
            .CountAsync(r => r.Liga.Temporada == temporada && r.Liga.Tipo == TipoCompetition.Copa && r.Numero > 0 && !r.Desempate, ct);

        return CalendarioTemporada.Montar(
            abertura,
            rodadasSerieA > 0 ? rodadasSerieA : 9,
            rodadasSerieB,
            rodadasGrupoCopa > 0 ? rodadasGrupoCopa : 4);
    }

    /// <summary>
    /// Calendário como está marcado: a conta a partir da abertura, com a data gravada nas rodadas
    /// que já têm uma (aplicada pelo calendário ou mudada à mão).
    /// </summary>
    public static async Task<IReadOnlyList<DataDaTemporada>?> MontarComDatasGravadasAsync(DraftDbContext db, int temporada, CancellationToken ct)
    {
        var calendario = await MontarAsync(db, temporada, ct);
        if (calendario is null) return null;

        var rodadas = await db.LigaRodadas.AsNoTracking()
            .Where(r => r.DataHora != null && r.Numero > 0 && !r.Desempate
                        && ((r.Liga.Temporada == temporada && r.Liga.Tipo != TipoCompetition.Supercopa)
                            || (r.Liga.Temporada == temporada - 1 && r.Liga.Tipo == TipoCompetition.Supercopa)))
            .Select(r => new { r.Liga.Tipo, r.Liga.Divisao, r.Numero, DataHora = r.DataHora!.Value })
            .ToListAsync(ct);

        return CalendarioTemporada.ComDatasGravadas(calendario, d =>
            d.Tipo == TipoCompetition.Supercopa
                ? rodadas.FirstOrDefault(r => r.Tipo == TipoCompetition.Supercopa)?.DataHora
                : d.Rodada is int numero
                    ? rodadas.FirstOrDefault(r => r.Tipo == d.Tipo && r.Divisao == d.Divisao && r.Numero == numero)?.DataHora
                    : null);
    }

    /// <summary>Rodadas de uma divisão = times - 1 (turno único).</summary>
    private static async Task<int> RodadasDaDivisaoAsync(DraftDbContext db, int temporada, Divisao divisao, CancellationToken ct)
    {
        var times = await db.LigaTimes
            .CountAsync(t => t.Liga.Temporada == temporada && t.Liga.Tipo == TipoCompetition.Liga && t.Liga.Divisao == divisao, ct);

        return times > 1 ? times - 1 : 0;
    }
}
