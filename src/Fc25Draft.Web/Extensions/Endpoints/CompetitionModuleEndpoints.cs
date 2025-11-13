using System.ComponentModel.DataAnnotations;
using System.Linq;
using Fc25Draft.Core.DTOs.Competitions;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Web.Extensions;
using Fc25Draft.Web.Models.Competitions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Extensions.Endpoints;

public static class CompetitionModuleEndpoints
{
    public static IEndpointRouteBuilder MapCompetitionModuleEndpoints(this IEndpointRouteBuilder api)
    {
        var module = api.MapGroup("/competition-module");

        module.MapGet("/competitions", GetCompetitionsAsync);
        module.MapGet("/competitions/{competitionId:guid}", GetCompetitionDetailsAsync);
        module.MapGet("/competitions/{competitionId:guid}/teams", GetTeamsAsync);
        module.MapGet("/competitions/{competitionId:guid}/rounds", GetRoundsAsync);
        module.MapGet("/competitions/{competitionId:guid}/standings", GetStandingsAsync);
        module.MapGet("/competitions/{competitionId:guid}/player-stats", GetPlayerStatsAsync);
        module.MapGet("/competitions/{competitionId:guid}/team-stats", GetTeamStatsAsync);
        module.MapGet("/matches/{matchId:guid}", GetMatchAsync);

        var adminCompetitions = module.MapGroup("/competitions").RequireAuthorization("AdminOnly");
        adminCompetitions.MapPost("", CreateCompetitionAsync);
        adminCompetitions.MapPut("/{competitionId:guid}", UpdateCompetitionAsync);
        adminCompetitions.MapPost("/{competitionId:guid}/activate", ToggleCompetitionAsync);
        adminCompetitions.MapPost("/{competitionId:guid}/teams", AddTeamAsync);
        adminCompetitions.MapDelete("/{competitionId:guid}/teams/{competitionTeamId:guid}", RemoveTeamAsync);
        adminCompetitions.MapPost("/{competitionId:guid}/rounds/generate", GenerateRoundsAsync);
        adminCompetitions.MapPost("/{competitionId:guid}/rebuild", RebuildStandingsEndpointAsync);

        var adminMatches = module.MapGroup("/matches").RequireAuthorization("AdminOnly");
        adminMatches.MapPost("", UpsertMatchAsync);
        adminMatches.MapPut("/{competitionMatchId:guid}", UpsertMatchAsync);
        adminMatches.MapDelete("/{competitionMatchId:guid}", DeleteMatchAsync);
        adminMatches.MapPut("/{competitionMatchId:guid}/events", ReplaceMatchEventsAsync);

        return api;
    }

    private static async Task<IResult> GetCompetitionsAsync(ICompetitionService service, CancellationToken ct)
    {
        try
        {
            var competitions = await service.GetCompetitionsAsync(ct).ConfigureAwait(false);
            return Results.Ok(competitions);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar as competições.");
        }
    }

    private static async Task<IResult> GetCompetitionDetailsAsync(Guid competitionId, ICompetitionService service, CancellationToken ct)
    {
        try
        {
            var details = await service.GetCompetitionDetailsAsync(competitionId, ct).ConfigureAwait(false);
            return Results.Ok(details);
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar os detalhes da competição.");
        }
    }

    private static async Task<IResult> CreateCompetitionAsync(
        HttpContext httpContext,
        [FromBody] CompetitionCreateRequest request,
        ICompetitionService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return EndpointHelpers.CreateValidationProblem("O nome é obrigatório.");
        }

        var command = new CompetitionCreateCommand(
            request.SeasonId,
            request.Name.Trim(),
            request.Order,
            request.Type ?? CompetitionType.League,
            request.IsActive);

        try
        {
            var result = await service.CreateCompetitionAsync(command, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return Results.Created($"/api/competition-module/competitions/{result.CompetitionId}", result);
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (DbUpdateException)
        {
            return EndpointHelpers.CreateConflictProblem("Já existe uma competição com os dados informados nesta temporada.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao criar a competição.");
        }
    }

    private static async Task<IResult> UpdateCompetitionAsync(
        HttpContext httpContext,
        Guid competitionId,
        [FromBody] CompetitionUpdateRequest request,
        ICompetitionService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return EndpointHelpers.CreateValidationProblem("O nome é obrigatório.");
        }

        var command = new CompetitionUpdateCommand(
            request.Name.Trim(),
            request.Order,
            request.Type ?? CompetitionType.League,
            request.IsActive);

        try
        {
            var updated = await service.UpdateCompetitionAsync(competitionId, command, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return updated is null
                ? EndpointHelpers.CreateNotFoundProblem("Competição não encontrada.")
                : Results.Ok(updated);
        }
        catch (DbUpdateException)
        {
            return EndpointHelpers.CreateConflictProblem("Já existe uma competição com os dados informados.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao atualizar a competição.");
        }
    }

    private static async Task<IResult> ToggleCompetitionAsync(
        HttpContext httpContext,
        Guid competitionId,
        [FromBody] CompetitionToggleRequest request,
        ICompetitionService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        try
        {
            var updated = await service.SetCompetitionActiveAsync(competitionId, request.IsActive!.Value, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return updated ? Results.NoContent() : EndpointHelpers.CreateNotFoundProblem("Competição não encontrada.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao atualizar o status da competição.");
        }
    }

    private static async Task<IResult> GetTeamsAsync(Guid competitionId, ICompetitionService service, CancellationToken ct)
    {
        try
        {
            var teams = await service.GetTeamsAsync(competitionId, ct).ConfigureAwait(false);
            return Results.Ok(teams);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar os times da competição.");
        }
    }

    private static async Task<IResult> AddTeamAsync(
        HttpContext httpContext,
        Guid competitionId,
        [FromBody] CompetitionTeamRequest request,
        ICompetitionService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        var command = new CompetitionTeamAssignCommand(
            request.TeamId!.Value,
            request.InitialBudget,
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());

        try
        {
            var team = await service.AddTeamAsync(competitionId, command, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return Results.Ok(team);
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return EndpointHelpers.CreateConflictProblem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao adicionar o time à competição.");
        }
    }

    private static async Task<IResult> RemoveTeamAsync(
        HttpContext httpContext,
        Guid competitionId,
        Guid competitionTeamId,
        ICompetitionService service,
        CancellationToken ct)
    {
        try
        {
            var removed = await service.RemoveTeamAsync(competitionTeamId, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return removed ? Results.NoContent() : EndpointHelpers.CreateNotFoundProblem("Time não encontrado na competição.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao remover o time da competição.");
        }
    }

    private static async Task<IResult> GenerateRoundsAsync(
        HttpContext httpContext,
        Guid competitionId,
        [FromBody] CompetitionRoundGenerationRequest request,
        ICompetitionService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        var command = new CompetitionRoundGenerationCommand(
            request.IncludeReturnLeg,
            request.FirstRoundDateUtc,
            request.DaysBetweenRounds);

        try
        {
            var rounds = await service.GenerateRoundsAsync(competitionId, command, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return Results.Ok(rounds);
        }
        catch (InvalidOperationException ex)
        {
            return EndpointHelpers.CreateValidationProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao gerar as rodadas.");
        }
    }

    private static async Task<IResult> RebuildStandingsEndpointAsync(
        HttpContext httpContext,
        Guid competitionId,
        ICompetitionService service,
        CancellationToken ct)
    {
        try
        {
            var standings = await service.RebuildStandingsAsync(competitionId, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return Results.Ok(standings);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao recalcular a classificação.");
        }
    }

    private static async Task<IResult> GetRoundsAsync(Guid competitionId, ICompetitionService service, CancellationToken ct)
    {
        try
        {
            var rounds = await service.GetRoundsAsync(competitionId, ct).ConfigureAwait(false);
            return Results.Ok(rounds);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar as rodadas da competição.");
        }
    }

    private static async Task<IResult> GetStandingsAsync(Guid competitionId, ICompetitionService service, CancellationToken ct)
    {
        try
        {
            var standings = await service.GetStandingsAsync(competitionId, ct).ConfigureAwait(false);
            return Results.Ok(standings);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar a classificação da competição.");
        }
    }

    private static async Task<IResult> GetPlayerStatsAsync(Guid competitionId, ICompetitionService service, CancellationToken ct)
    {
        try
        {
            var stats = await service.GetPlayerStatsAsync(competitionId, ct).ConfigureAwait(false);
            return Results.Ok(stats);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar as estatísticas de jogadores.");
        }
    }

    private static async Task<IResult> GetTeamStatsAsync(Guid competitionId, ICompetitionService service, CancellationToken ct)
    {
        try
        {
            var stats = await service.GetTeamStatsAsync(competitionId, ct).ConfigureAwait(false);
            return Results.Ok(stats);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar as estatísticas de times.");
        }
    }

    private static async Task<IResult> GetMatchAsync(Guid matchId, ICompetitionService service, CancellationToken ct)
    {
        try
        {
            var match = await service.GetMatchDetailsAsync(matchId, ct).ConfigureAwait(false);
            return match is null ? EndpointHelpers.CreateNotFoundProblem("Partida não encontrada.") : Results.Ok(match);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar os dados da partida.");
        }
    }

    private static async Task<IResult> UpsertMatchAsync(
        HttpContext httpContext,
        Guid? competitionMatchId,
        [FromBody] CompetitionMatchUpsertRequest request,
        ICompetitionService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        var command = new CompetitionMatchUpsertCommand(
            competitionMatchId ?? request.CompetitionMatchId ?? Guid.NewGuid(),
            request.CompetitionId!.Value,
            request.RoundId!.Value,
            request.HomeCompetitionTeamId!.Value,
            request.AwayCompetitionTeamId!.Value,
            request.MatchDateUtc,
            request.HomeGoals,
            request.AwayGoals,
            request.Status ?? CompetitionMatchStatus.Scheduled,
            string.IsNullOrWhiteSpace(request.Stadium) ? null : request.Stadium.Trim(),
            string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim());

        try
        {
            var match = await service.UpsertMatchAsync(command, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return Results.Ok(match);
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao salvar os dados da partida.");
        }
    }

    private static async Task<IResult> DeleteMatchAsync(
        HttpContext httpContext,
        Guid competitionMatchId,
        ICompetitionService service,
        CancellationToken ct)
    {
        try
        {
            var removed = await service.DeleteMatchAsync(competitionMatchId, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return removed ? Results.NoContent() : EndpointHelpers.CreateNotFoundProblem("Partida não encontrada.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao excluir a partida.");
        }
    }

    private static async Task<IResult> ReplaceMatchEventsAsync(
        HttpContext httpContext,
        Guid competitionMatchId,
        [FromBody] CompetitionMatchEventsRequest request,
        ICompetitionService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        var events = request.Events
            .Select(e => new CompetitionMatchEventUpsertCommand(
                e.CompetitionMatchEventId,
                e.CompetitionTeamId!.Value,
                e.PlayerId,
                e.RelatedPlayerId,
                e.EventType!.Value,
                e.Minute,
                string.IsNullOrWhiteSpace(e.Observations) ? null : e.Observations.Trim()))
            .ToList();

        try
        {
            var updated = await service.ReplaceMatchEventsAsync(competitionMatchId, events, httpContext.User?.Identity?.Name, ct).ConfigureAwait(false);
            return Results.Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao atualizar os eventos da partida.");
        }
    }

    private static bool TryValidate(object model, out IResult? errorResult)
    {
        var context = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(model, context, validationResults, true))
        {
            errorResult = null;
            return true;
        }

        var errors = validationResults
            .GroupBy(v => v.MemberNames.FirstOrDefault() ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage ?? "Valor inválido.").ToArray());
        errorResult = Results.ValidationProblem(errors);
        return false;
    }
}
