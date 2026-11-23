using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Uma data do calendário da temporada.</summary>
/// <param name="Numero">Posição no calendário (1 = abertura).</param>
/// <param name="Rodada">Número da rodada da competição, quando for rodada de liga ou de grupos.</param>
/// <param name="EhJogo">Falso nas datas de janela de transferências.</param>
public record DataDaTemporada(
    int Numero,
    DateTime Quando,
    TipoCompetition? Tipo,
    Divisao? Divisao,
    int? Rodada,
    string Titulo,
    string Detalhe,
    bool EhJogo,
    DateTime? Abre = null);

/// <summary>
/// Calendário real da temporada, montado a partir da data da Supercopa (a abertura).
/// A janela de transferências fecha na véspera da Supercopa e a Série A estreia sozinha no sábado
/// seguinte. Da semana depois em diante o ritmo é fixo: <b>terça</b> Série B, <b>quinta</b> Série A e
/// <b>sábado</b> Copa. A Série A tem mais rodadas que a B, então termina depois; a última data é o
/// playoff de acesso, no sábado seguinte ao último jogo.
/// </summary>
public static class CalendarioTemporada
{
    /// <summary>Horário dos jogos.</summary>
    public static readonly TimeSpan Horario = new(18, 0, 0);

    /// <summary>Horário em que a janela de transferências abre, depois dos jogos.</summary>
    public static readonly TimeSpan HorarioDaJanela = new(20, 0, 0);

    /// <summary>Dias da semana de cada competição no ritmo fixo.</summary>
    public const DayOfWeek DiaDaSerieB = DayOfWeek.Tuesday, DiaDaSerieA = DayOfWeek.Thursday, DiaDaCopa = DayOfWeek.Saturday;

    /// <summary>Detalhe das datas do mata-mata da Copa (que não têm número de rodada).</summary>
    public const string QuartasDaCopa = "Quartas de final", SemifinaisDaCopa = "Semifinais", FinalDaCopa = "Final";

    public static IReadOnlyList<DataDaTemporada> Montar(
        DateTime abertura,
        int rodadasSerieA = 9,
        int rodadasSerieB = 7,
        int rodadasGrupoCopa = 4)
    {
        var datas = new List<DataDaTemporada>();
        void Add(DateTime dia, TipoCompetition? tipo, Divisao? divisao, int? rodada, string titulo, string detalhe,
                 bool ehJogo = true, DateTime? abre = null) =>
            datas.Add(new DataDaTemporada(datas.Count + 1, dia.Date + Horario, tipo, divisao, rodada, titulo, detalhe, ehJogo, abre));

        // A janela fecha na véspera da Supercopa, que abre a temporada; a Série A estreia no sábado
        // seguinte, antes de o ritmo da semana começar.
        var diaDaAbertura = abertura.Date;
        var estreiaDaSerieA = ProximoDia(diaDaAbertura, DiaDaCopa);

        Add(diaDaAbertura.AddDays(-1), null, null, null, "Mercado", "fecha a janela", ehJogo: false);
        Add(diaDaAbertura, TipoCompetition.Supercopa, null, null, "Supercopa", "abre a temporada");

        int serieA = 1, serieB = 1;
        if (rodadasSerieA > 0)
            Add(estreiaDaSerieA, TipoCompetition.Liga, Divisao.SerieA, serieA, "Série A", $"Rodada {serieA++}");

        // Datas da Copa na ordem em que acontecem, uma por sábado.
        var daCopa = new Queue<(string Titulo, string Detalhe, int? Rodada)>();
        for (int g = 1; g <= rodadasGrupoCopa; g++) daCopa.Enqueue(("Copa", $"Rodada {g} · grupos", g));
        daCopa.Enqueue(("Copa", QuartasDaCopa, null));
        daCopa.Enqueue(("Copa", SemifinaisDaCopa, null));
        daCopa.Enqueue(("Copa", FinalDaCopa, null));

        // Semana a semana, de terça a sábado, até acabar tudo.
        var terca = ProximoDia(estreiaDaSerieA, DiaDaSerieB);
        while (serieB <= rodadasSerieB || serieA <= rodadasSerieA || daCopa.Count > 0)
        {
            if (serieB <= rodadasSerieB)
                Add(terca, TipoCompetition.Liga, Divisao.SerieB, serieB, "Série B", $"Rodada {serieB++}");
            if (serieA <= rodadasSerieA)
                Add(ProximoDia(terca, DiaDaSerieA), TipoCompetition.Liga, Divisao.SerieA, serieA, "Série A", $"Rodada {serieA++}");
            if (daCopa.Count > 0)
            {
                var (titulo, detalhe, rodada) = daCopa.Dequeue();
                Add(ProximoDia(terca, DiaDaCopa), TipoCompetition.Copa, null, rodada, titulo, detalhe);
            }

            terca = terca.AddDays(7);
        }

        // Fecha a temporada com o playoff, no sábado seguinte ao último jogo.
        var ultimoJogo = datas.Where(d => d.EhJogo).Max(d => d.Quando);
        Add(ProximoDia(ultimoJogo.Date, DiaDaCopa), TipoCompetition.Liga, null, null, "Playoff de acesso", "");

        return datas;
    }

    /// <summary>
    /// Troca a data das rodadas que já têm data gravada (mudada à mão ou aplicada antes) e põe o
    /// calendário na ordem cronológica, renumerando. O mata-mata da Copa não tem data própria: segue
    /// o dia da semana e a hora da última rodada dos grupos, uma fase por semana. O resto (mercado,
    /// playoff) segue a conta a partir da abertura.
    /// </summary>
    public static IReadOnlyList<DataDaTemporada> ComDatasGravadas(
        IEnumerable<DataDaTemporada> calendario, Func<DataDaTemporada, DateTime?> dataGravada)
    {
        var datas = calendario
            .Select(d => dataGravada(d) is DateTime gravada ? d with { Quando = gravada } : d)
            .ToList();

        var ultimaDosGrupos = datas
            .Where(d => d.Tipo == TipoCompetition.Copa && d.Rodada is not null)
            .MaxBy(d => d.Rodada);
        if (ultimaDosGrupos is not null)
        {
            var semana = 0;
            datas = datas
                .Select(d => d.Tipo == TipoCompetition.Copa && d.Rodada is null
                    ? d with { Quando = ultimaDosGrupos.Quando.AddDays(7 * ++semana) }
                    : d)
                .ToList();
        }

        return datas
            .OrderBy(d => d.Quando)
            .ThenBy(d => d.Numero)
            .Select((d, i) => d with { Numero = i + 1 })
            .ToList();
    }

    /// <summary>Datas de uma competição da temporada, na ordem das rodadas.</summary>
    public static IReadOnlyList<DateTime> DatasDasRodadas(
        IEnumerable<DataDaTemporada> calendario, TipoCompetition tipo, Divisao? divisao) =>
        calendario
            .Where(d => d.Tipo == tipo && d.Divisao == divisao && d.Rodada is not null)
            .OrderBy(d => d.Rodada)
            .Select(d => d.Quando)
            .ToList();

    /// <summary>Primeiro dia da semana pedido depois da data (nunca a própria data).</summary>
    private static DateTime ProximoDia(DateTime depoisDe, DayOfWeek dia)
    {
        var dias = ((int)dia - (int)depoisDe.DayOfWeek + 7) % 7;
        return depoisDe.AddDays(dias == 0 ? 7 : dias);
    }

}
