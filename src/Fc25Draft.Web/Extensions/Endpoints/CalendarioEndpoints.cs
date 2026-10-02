using Fc25Draft.Core.Interfaces;

namespace Fc25Draft.Web.Extensions.Endpoints;

/// <summary>Calendário dos jogos de cada time (.ics), público para os apps de agenda conseguirem buscar.</summary>
public static class CalendarioEndpoints
{
    public static IEndpointRouteBuilder MapCalendarioEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/calendario/time/{timeId:guid}.ics", async (
            Guid timeId, HttpContext ctx, ICalendarioService calendario, CancellationToken ct) =>
        {
            var site = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var ics = await calendario.IcsDoTimeAsync(timeId, site, ct);
            if (ics is null) return Results.NotFound();

            // Os apps de agenda buscam de novo sozinhos; 15 min de cache poupa o banco sem atrasar o placar.
            ctx.Response.Headers.CacheControl = "public, max-age=900";
            ctx.Response.Headers.ContentDisposition = "inline; filename=\"cbfv-jogos.ics\"";
            return Results.Text(ics, "text/calendar; charset=utf-8");
        }).AllowAnonymous();

        return app;
    }
}
