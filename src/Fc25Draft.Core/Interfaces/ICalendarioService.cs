namespace Fc25Draft.Core.Interfaces;

/// <summary>Calendário dos jogos do time no formato iCalendar (.ics), para assinar no celular.</summary>
public interface ICalendarioService
{
    /// <summary>
    /// Os jogos do time com dia marcado (os últimos meses e os que vêm), com placar quando já foi jogado.
    /// Nulo quando o time não existe. <paramref name="urlDoSite"/> vai no link de cada evento.
    /// </summary>
    Task<string?> IcsDoTimeAsync(Guid timeId, string urlDoSite, CancellationToken ct);
}
