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
    bool EhJogo);

/// <summary>
/// Calendário real da temporada, montado a partir do domingo de abertura (Supercopa).
/// O ritmo é semanal e fixo: <b>terça</b> Série B, <b>quarta</b> Série A e <b>sexta</b> Copa.
/// Como a Série A tem mais rodadas que a B, no meio da temporada entra uma semana em que a B
/// folga e a A joga duas vezes (logo depois da janela de transferências) — assim as duas séries
/// terminam na mesma semana, a B um dia antes da A. A última data é o playoff de acesso.
/// </summary>
public static class CalendarioTemporada
{
    /// <summary>Horário dos jogos.</summary>
    public static readonly TimeSpan Horario = new(21, 0, 0);

    /// <summary>
    /// Abertura combinada da temporada: domingo 27/09, com a final da Supercopa. É daqui que sai
    /// todo o resto do calendário — para mudar de temporada, basta trocar esta data.
    /// </summary>
    public static readonly DateTime Abertura = new(2026, 9, 27);

    public static IReadOnlyList<DataDaTemporada> Montar(
        DateTime abertura,
        int rodadasSerieA = 9,
        int rodadasSerieB = 7,
        int rodadasGrupoCopa = 4)
    {
        var datas = new List<DataDaTemporada>();
        void Add(DateTime dia, TipoCompetition? tipo, Divisao? divisao, int? rodada, string titulo, string detalhe, bool ehJogo = true) =>
            datas.Add(new DataDaTemporada(datas.Count + 1, dia.Date + Horario, tipo, divisao, rodada, titulo, detalhe, ehJogo));

        // A temporada abre na data da Supercopa, seja ela qual for; a primeira rodada é na terça
        // seguinte e a janela fecha na véspera.
        var diaDaAbertura = abertura.Date;
        var primeiraTerca = ProximoDia(diaDaAbertura, DayOfWeek.Tuesday);

        Add(diaDaAbertura, TipoCompetition.Supercopa, null, null, "Supercopa", "abre a temporada");
        Add(primeiraTerca.AddDays(-1), null, null, null, "Mercado", "fecha a janela", ehJogo: false);

        var extrasDaSerieA = Math.Max(0, rodadasSerieA - rodadasSerieB);
        var semanas = rodadasSerieB + (extrasDaSerieA > 0 ? 1 : 0);
        var semanaDoMercado = extrasDaSerieA > 0 ? (semanas + 1) / 2 : 0;

        // Datas da Copa na ordem em que acontecem, uma por sexta.
        var daCopa = new Queue<(string Titulo, string Detalhe, int? Rodada)>();
        for (int g = 1; g <= rodadasGrupoCopa; g++) daCopa.Enqueue(("Copa", $"Rodada {g} · grupos", g));
        daCopa.Enqueue(("Copa", "Quartas de final", null));
        daCopa.Enqueue(("Copa", "Semifinais", null));
        daCopa.Enqueue(("Copa", "Final", null));

        int serieA = 1, serieB = 1;

        for (int semana = 1; semana <= semanas; semana++)
        {
            var terca = primeiraTerca.AddDays((semana - 1) * 7);
            var quarta = terca.AddDays(1);
            var sexta = terca.AddDays(3);

            if (semana == semanaDoMercado)
            {
                // Janela do meio na segunda; a Série B folga e a Série A joga as rodadas que sobram.
                Add(terca.AddDays(-1), null, null, null, "Mercado", "fecha a janela do meio", ehJogo: false);

                for (int i = 0; i < extrasDaSerieA && serieA <= rodadasSerieA; i++)
                    Add(terca.AddDays(i), TipoCompetition.Liga, Divisao.SerieA, serieA, "Série A", $"Rodada {serieA++}");
            }
            else
            {
                if (serieB <= rodadasSerieB)
                    Add(terca, TipoCompetition.Liga, Divisao.SerieB, serieB, "Série B", $"Rodada {serieB++}");
                if (serieA <= rodadasSerieA)
                    Add(quarta, TipoCompetition.Liga, Divisao.SerieA, serieA, "Série A", $"Rodada {serieA++}");
            }

            if (daCopa.Count > 0)
            {
                var (titulo, detalhe, rodada) = daCopa.Dequeue();
                Add(sexta, TipoCompetition.Copa, null, rodada, titulo, detalhe);
            }
        }

        // O que sobrar (rodadas de liga ou da Copa) segue nas semanas seguintes, no mesmo ritmo.
        var extra = semanas;
        while (serieB <= rodadasSerieB || serieA <= rodadasSerieA || daCopa.Count > 0)
        {
            extra++;
            var terca = primeiraTerca.AddDays((extra - 1) * 7);

            if (serieB <= rodadasSerieB)
                Add(terca, TipoCompetition.Liga, Divisao.SerieB, serieB, "Série B", $"Rodada {serieB++}");
            if (serieA <= rodadasSerieA)
                Add(terca.AddDays(1), TipoCompetition.Liga, Divisao.SerieA, serieA, "Série A", $"Rodada {serieA++}");
            if (daCopa.Count > 0)
            {
                var (titulo, detalhe, rodada) = daCopa.Dequeue();
                Add(terca.AddDays(3), TipoCompetition.Copa, null, rodada, titulo, detalhe);
            }
        }

        // Fecha a temporada com o playoff, na sexta seguinte à última rodada de liga.
        var ultimoJogo = datas.Where(d => d.EhJogo).Max(d => d.Quando);
        var sextaFinal = ProximaSexta(ultimoJogo.Date);
        Add(sextaFinal, TipoCompetition.Liga, null, null, "Playoff de acesso", "9º da Série A x 2º da Série B");

        return datas;
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

    private static DateTime ProximaSexta(DateTime depoisDe) => ProximoDia(depoisDe, DayOfWeek.Friday);
}
