using System.Text;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Monta o .ics (RFC 5545) com os jogos do time. O horário da rodada é gravado em hora de Brasília; no arquivo
/// vai em UTC, e cada app de agenda mostra no fuso de quem assina. O app busca de novo de tempos em tempos,
/// então jogo remarcado e placar aparecem sozinhos.
/// </summary>
public class CalendarioService : ICalendarioService
{
    /// <summary>Jogos mais antigos que isso não entram (a agenda não precisa da temporada inteira de anos atrás).</summary>
    private static readonly TimeSpan Passado = TimeSpan.FromDays(120);

    private static readonly TimeSpan Duracao = TimeSpan.FromHours(1);

    private readonly DraftDbContext _db;
    private readonly TimeProvider _time;

    public CalendarioService(DraftDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<string?> IcsDoTimeAsync(Guid timeId, string urlDoSite, CancellationToken ct)
    {
        var timeNome = await _db.Teams.AsNoTracking().Where(t => t.TeamId == timeId).Select(t => t.TeamName).FirstOrDefaultAsync(ct);
        if (timeNome is null) return null;

        var agoraUtc = _time.GetUtcNow().UtcDateTime;
        var desde = HorarioDeBrasilia.Agora(_time) - Passado;

        var jogos = await _db.LigaPartidas.AsNoTracking()
            .Where(p => (p.TimeCasaId == timeId || p.TimeForaId == timeId)
                        && p.Rodada.DataHora != null && p.Rodada.DataHora >= desde)
            .OrderBy(p => p.Rodada.DataHora)
            .Select(p => new
            {
                p.PartidaId, p.Rodada.LigaId, Liga = p.Rodada.Liga.Nome, p.Rodada.Numero, p.Rodada.Desempate,
                DataHora = p.Rodada.DataHora!.Value,
                Casa = p.TimeCasa.TeamName, Fora = p.TimeFora.TeamName, p.GolsCasa, p.GolsFora, p.Status
            })
            .ToListAsync(ct);

        var site = urlDoSite.TrimEnd('/');
        var ics = new StringBuilder();
        void Linha(string texto) => Dobrar(ics, texto);

        Linha("BEGIN:VCALENDAR");
        Linha("VERSION:2.0");
        Linha("PRODID:-//CBFV//Calendario dos jogos//PT-BR");
        Linha("CALSCALE:GREGORIAN");
        Linha("METHOD:PUBLISH");
        Linha($"X-WR-CALNAME:{Escapar($"CBFV · {timeNome}")}");
        Linha($"X-WR-CALDESC:{Escapar($"Jogos do {timeNome} na CBFV")}");
        Linha("X-WR-TIMEZONE:America/Sao_Paulo");
        Linha("REFRESH-INTERVAL;VALUE=DURATION:PT1H");
        Linha("X-PUBLISHED-TTL:PT1H");

        foreach (var j in jogos)
        {
            var inicio = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(j.DataHora, DateTimeKind.Unspecified), HorarioDeBrasilia.Fuso);
            var etapa = j.Desempate ? "Jogo decisivo" : j.Numero > 0 ? $"Rodada {j.Numero}" : "Mata-mata";
            var placar = j.Status == PartidaStatus.Encerrada ? $" ({j.GolsCasa}–{j.GolsFora})" : "";
            var link = $"{site}/liga?liga={j.LigaId}";

            Linha("BEGIN:VEVENT");
            Linha($"UID:{j.PartidaId}@cbfv");
            Linha($"DTSTAMP:{Utc(agoraUtc)}");
            Linha($"DTSTART:{Utc(inicio)}");
            Linha($"DTEND:{Utc(inicio + Duracao)}");
            Linha($"SUMMARY:{Escapar($"⚽ {j.Casa} x {j.Fora}{placar}")}");
            Linha($"DESCRIPTION:{Escapar($"{j.Liga} · {etapa}\n{link}")}");
            Linha($"URL:{link}");
            Linha("STATUS:CONFIRMED");
            Linha("TRANSP:OPAQUE");
            Linha("END:VEVENT");
        }

        Linha("END:VCALENDAR");
        return ics.ToString();
    }

    private static string Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'");

    // Texto de propriedade: barra, ponto e vírgula, vírgula e quebra de linha são escapados.
    private static string Escapar(string texto) => texto
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

    /// <summary>Linhas com mais de 75 bytes são dobradas (continuação começa com espaço), sem partir caractere UTF-8.</summary>
    private static void Dobrar(StringBuilder ics, string linha)
    {
        var bytes = 0;
        var limite = 75;
        foreach (var r in linha.EnumerateRunes())
        {
            var tamanho = r.Utf8SequenceLength;
            if (bytes + tamanho > limite)
            {
                ics.Append("\r\n ");
                bytes = 1;
                limite = 75;
            }
            ics.Append(r.ToString());
            bytes += tamanho;
        }
        ics.Append("\r\n");
    }
}
