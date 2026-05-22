using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fc25Draft.Web.Extensions.Endpoints;

public static class LigaEndpoints
{
    public static IEndpointRouteBuilder MapLigaEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Público ───────────────────────────────────────────────────────────
        var pub = app.MapGroup("/liga");

        pub.MapGet("/atual", async (ILigaPublicService svc, CancellationToken ct) =>
        {
            var liga = await svc.GetAtualAsync(ct);
            return liga is null ? Results.NotFound(new { message = "Nenhuma liga encontrada." }) : Results.Ok(liga);
        });

        pub.MapGet("/ativas", async (ILigaPublicService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAtivasAsync(ct)));

        pub.MapGet("/{ligaId:guid}/classificacao", async (Guid ligaId, ILigaPublicService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetClassificacaoAsync(ligaId, ct)));

        pub.MapGet("/{ligaId:guid}/artilheiros", async (Guid ligaId, ILigaPublicService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetArtilheirosAsync(ligaId, ct)));

        pub.MapGet("/{ligaId:guid}/cartoes", async (Guid ligaId, ILigaPublicService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCartoesEstatAsync(ligaId, ct)));

        pub.MapGet("/{ligaId:guid}/knockout", async (Guid ligaId, ILigaPublicService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetKnockoutAsync(ligaId, ct)));

        pub.MapGet("/{ligaId:guid}/rodadas", async (Guid ligaId, ILigaPublicService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRodadasComPartidasAsync(ligaId, ct)));

        pub.MapGet("/{ligaId:guid}/grupos", async (Guid ligaId, ILigaPublicService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetGruposAsync(ligaId, ct)));

        // ── Admin ─────────────────────────────────────────────────────────────
        var admin = app.MapGroup("/admin/liga").RequireAuthorization("AdminOnly");

        // Liga CRUD
        admin.MapGet("", async (ILigaAdminService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(ct)));
        admin.MapGet("/{ligaId:guid}", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            var liga = await svc.GetByIdAsync(ligaId, ct);
            return liga is null ? Results.NotFound(new { message = "Liga não encontrada." }) : Results.Ok(liga);
        });
        admin.MapPost("", async (LigaCreateRequest request, ILigaAdminService svc, CancellationToken ct) =>
        {
            try
            {
                var liga = await svc.CreateAsync(request, ct);
                return Results.Created($"/api/admin/liga/{liga.LigaId}", liga);
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        admin.MapPut("/{ligaId:guid}", async (Guid ligaId, LigaUpdateRequest request, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.UpdateAsync(ligaId, request, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        admin.MapPost("/{ligaId:guid}:iniciar", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.IniciarPrimeiraFaseAsync(ligaId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        admin.MapPost("/{ligaId:guid}:encerrar-fase", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.EncerrarPrimeiraFaseAsync(ligaId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        // Rodadas
        var rodadas = admin.MapGroup("/{ligaId:guid}/rodadas");
        rodadas.MapGet("", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListRodadasAsync(ligaId, ct)));
        rodadas.MapPost("", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.CreateRodadaAsync(ligaId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        rodadas.MapPost("/gerar-auto", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.GerarRodadasAutoAsync(ligaId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        rodadas.MapDelete("/{rodadaId:guid}", async (Guid ligaId, Guid rodadaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { await svc.DeleteRodadaAsync(rodadaId, ct); return Results.NoContent(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        // Partidas
        var partidas = admin.MapGroup("/rodadas/{rodadaId:guid}/partidas");
        partidas.MapGet("", async (Guid rodadaId, ILigaAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPartidasAsync(rodadaId, ct)));
        partidas.MapPost("", async (Guid rodadaId, LigaPartidaCreateRequest request, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.CreatePartidaAsync(rodadaId, request, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        var partidaActions = admin.MapGroup("/partidas/{partidaId:guid}");
        partidaActions.MapPost(":iniciar", async (Guid partidaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.IniciarPartidaAsync(partidaId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        partidaActions.MapPost(":encerrar", async (Guid partidaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.EncerrarPartidaAsync(partidaId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        partidaActions.MapPost(":wo", async (Guid partidaId, [FromBody] WORequest request, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.AplicarWOAsync(partidaId, request.TimeWOId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        partidaActions.MapDelete("", async (Guid partidaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { await svc.DeletePartidaAsync(partidaId, ct); return Results.NoContent(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        // Eventos (gols e cartões)
        var eventos = admin.MapGroup("/partidas/{partidaId:guid}/eventos");
        eventos.MapGet("", async (Guid partidaId, ILigaAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListEventosAsync(partidaId, ct)));
        eventos.MapPost("/gol", async (Guid partidaId, LigaGolRequest request, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.AddGolAsync(partidaId, request, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        eventos.MapPost("/cartao", async (Guid partidaId, LigaCartaoRequest request, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.AddCartaoAsync(partidaId, request, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        eventos.MapDelete("/{eventoId:guid}", async (Guid partidaId, Guid eventoId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { await svc.DeleteEventoAsync(eventoId, ct); return Results.NoContent(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        // Punições
        var punicoes = admin.MapGroup("/{ligaId:guid}/punicoes");
        punicoes.MapGet("", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPunicoesAsync(ligaId, ct)));
        punicoes.MapPost("", async (Guid ligaId, LigaPunicaoRequest request, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.AplicarPunicaoAsync(ligaId, request, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        punicoes.MapDelete("/{punicaoId:guid}", async (Guid ligaId, Guid punicaoId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { await svc.RemoverPunicaoAsync(punicaoId, ct); return Results.NoContent(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        // Copa grupos
        admin.MapGet("/{ligaId:guid}/grupos", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListGruposAsync(ligaId, ct)));
        admin.MapPost("/{ligaId:guid}/grupos", async (Guid ligaId, LigaConfigurarGruposRequest request, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { await svc.ConfigurarGruposCopaAsync(ligaId, request, ct); return Results.NoContent(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        // Tiebreaker
        admin.MapPost("/{ligaId:guid}:iniciar-decisao", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.IniciarDecisaoCampeaoAsync(ligaId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        admin.MapPost("/{ligaId:guid}:iniciar-mini-liga", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.IniciarMiniLigaAsync(ligaId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        // Knockout
        var knockout = admin.MapGroup("/{ligaId:guid}/knockout");
        knockout.MapPost("/gerar", async (Guid ligaId, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.GerarFaseKnockoutAsync(ligaId, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });
        knockout.MapPost("/jogos/{knockoutJogoId:guid}:encerrar", async (Guid ligaId, Guid knockoutJogoId, LigaEncerrarKnockoutRequest request, ILigaAdminService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.EncerrarKnockoutJogoAsync(knockoutJogoId, request, ct)); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
        });

        return app;
    }
}

public record WORequest(Guid TimeWOId);
