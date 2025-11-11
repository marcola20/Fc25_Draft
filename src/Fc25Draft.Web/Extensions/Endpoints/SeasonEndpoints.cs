using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Fc25Draft.Core.DTOs.Seasons;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Web.Extensions;
using Fc25Draft.Web.Models.Calendar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Extensions.Endpoints;

public static class SeasonEndpoints
{
    public static IEndpointRouteBuilder MapSeasonEndpoints(this IEndpointRouteBuilder api)
    {
        var seasons = api.MapGroup("/seasons");
        seasons.MapGet("", GetSeasonsAsync);
        seasons.MapGet("/{seasonId:guid}/competitions", GetCompetitionsAsync);
        seasons.MapGet("/{seasonId:guid}/schedule", GetScheduleAsync);

        var seasonsAdmin = seasons.MapGroup(string.Empty).RequireAuthorization("AdminOnly");
        seasonsAdmin.MapPost("", CreateSeasonAsync);
        seasonsAdmin.MapPut("/{seasonId:guid}", UpdateSeasonAsync);
        seasonsAdmin.MapDelete("/{seasonId:guid}", DeleteSeasonAsync);
        seasonsAdmin.MapPost("/{seasonId:guid}/competitions", CreateCompetitionAsync);
        seasonsAdmin.MapPut("/{seasonId:guid}/schedule", UpdateSeasonScheduleAsync);

        var competitions = api.MapGroup("/competitions");
        competitions.MapGet("/{competitionId:guid}/rounds", GetRoundsAsync);
        var competitionsAdmin = competitions.MapGroup(string.Empty).RequireAuthorization("AdminOnly");
        competitionsAdmin.MapPut("/{competitionId:guid}", UpdateCompetitionAsync);
        competitionsAdmin.MapDelete("/{competitionId:guid}", DeleteCompetitionAsync);
        competitionsAdmin.MapPost("/{competitionId:guid}/rounds", CreateRoundAsync);

        var rounds = api.MapGroup("/rounds");
        rounds.MapGet("/{roundId:guid}/selection", GetRoundSelectionAsync);

        var roundsAdmin = rounds.MapGroup(string.Empty).RequireAuthorization("AdminOnly");
        roundsAdmin.MapPut("/{roundId:guid}", UpdateRoundAsync);
        roundsAdmin.MapDelete("/{roundId:guid}", DeleteRoundAsync);
        roundsAdmin.MapPost("/{roundId:guid}/complete", CompleteRoundAsync);
        roundsAdmin.MapPost("/{roundId:guid}/selection/players", AddPlayersToSelectionAsync);
        roundsAdmin.MapDelete("/{roundId:guid}/selection/players/{playerId:guid}", RemovePlayerFromSelectionAsync);

        return api;
    }

    private static async Task<IResult> GetSeasonsAsync(ISeasonQueryService service, CancellationToken ct)
    {
        try
        {
            var seasons = await service.GetSeasonsAsync(ct).ConfigureAwait(false);
            return Results.Ok(seasons);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar as temporadas.");
        }
    }

    private static async Task<IResult> GetCompetitionsAsync(Guid seasonId, ISeasonQueryService service, CancellationToken ct)
    {
        try
        {
            var competitions = await service.GetCompetitionsAsync(seasonId, ct).ConfigureAwait(false);
            return Results.Ok(competitions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar as competições.");
        }
    }

    private static async Task<IResult> GetRoundsAsync(Guid competitionId, ISeasonQueryService service, CancellationToken ct)
    {
        try
        {
            var rounds = await service.GetRoundsAsync(competitionId, ct).ConfigureAwait(false);
            return Results.Ok(rounds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar as rodadas.");
        }
    }

    private static async Task<IResult> GetScheduleAsync(Guid seasonId, ISeasonQueryService service, CancellationToken ct)
    {
        try
        {
            var schedule = await service.GetScheduleAsync(seasonId, ct).ConfigureAwait(false);
            return Results.Ok(schedule);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar o calendário da temporada.");
        }
    }

    private static async Task<IResult> CreateSeasonAsync([
        FromBody] SeasonUpsertRequest request,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            var name = request.Name!.Trim();
            if (name.Length < 3)
            {
                return EndpointHelpers.CreateValidationProblem("O nome da temporada deve conter ao menos 3 caracteres úteis.");
            }

            var command = new SeasonUpsertCommand(name, request.IsActive);
            var season = await service.CreateSeasonAsync(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/seasons/{season.SeasonId}", season);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return EndpointHelpers.CreateConflictProblem("Já existe uma temporada com os dados informados.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao criar a temporada.");
        }
    }

    private static async Task<IResult> UpdateSeasonAsync(
        Guid seasonId,
        [FromBody] SeasonUpsertRequest request,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            var name = request.Name!.Trim();
            if (name.Length < 3)
            {
                return EndpointHelpers.CreateValidationProblem("O nome da temporada deve conter ao menos 3 caracteres úteis.");
            }

            var command = new SeasonUpsertCommand(name, request.IsActive);
            var season = await service.UpdateSeasonAsync(seasonId, command, ct).ConfigureAwait(false);
            return season is null
                ? EndpointHelpers.CreateNotFoundProblem("Temporada não encontrada.")
                : Results.Ok(season);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return EndpointHelpers.CreateConflictProblem("Já existe uma temporada com os dados informados.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao atualizar a temporada.");
        }
    }

    private static async Task<IResult> DeleteSeasonAsync(
        Guid seasonId,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        try
        {
            var deleted = await service.DeleteSeasonAsync(seasonId, ct).ConfigureAwait(false);
            return deleted ? Results.NoContent() : EndpointHelpers.CreateNotFoundProblem("Temporada não encontrada.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao remover a temporada.");
        }
    }

    private static async Task<IResult> CreateCompetitionAsync(
        Guid seasonId,
        [FromBody] CompetitionUpsertRequest request,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            var name = request.Name!.Trim();
            if (name.Length < 3)
            {
                return EndpointHelpers.CreateValidationProblem("O nome da competição deve conter ao menos 3 caracteres úteis.");
            }

            var command = new CompetitionUpsertCommand(name, request.Order, request.IsActive);
            var competition = await service.CreateCompetitionAsync(seasonId, command, ct).ConfigureAwait(false);
            return Results.Created($"/api/competitions/{competition.CompetitionId}", competition);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (DbUpdateException)
        {
            return EndpointHelpers.CreateConflictProblem("Já existe uma competição com os dados informados.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao criar a competição.");
        }
    }

    private static async Task<IResult> UpdateCompetitionAsync(
        Guid competitionId,
        [FromBody] CompetitionUpsertRequest request,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            var name = request.Name!.Trim();
            if (name.Length < 3)
            {
                return EndpointHelpers.CreateValidationProblem("O nome da competição deve conter ao menos 3 caracteres úteis.");
            }

            var command = new CompetitionUpsertCommand(name, request.Order, request.IsActive);
            var competition = await service.UpdateCompetitionAsync(competitionId, command, ct).ConfigureAwait(false);
            return competition is null
                ? EndpointHelpers.CreateNotFoundProblem("Competição não encontrada.")
                : Results.Ok(competition);
        }
        catch (OperationCanceledException)
        {
            throw;
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

    private static async Task<IResult> DeleteCompetitionAsync(
        Guid competitionId,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        try
        {
            var deleted = await service.DeleteCompetitionAsync(competitionId, ct).ConfigureAwait(false);
            return deleted ? Results.NoContent() : EndpointHelpers.CreateNotFoundProblem("Competição não encontrada.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao remover a competição.");
        }
    }

    private static async Task<IResult> CreateRoundAsync(
        Guid competitionId,
        [FromBody] RoundUpsertRequest request,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            var name = request.Name!.Trim();
            if (name.Length < 3)
            {
                return EndpointHelpers.CreateValidationProblem("O nome da rodada deve conter ao menos 3 caracteres úteis.");
            }

            var playedAtUtc = request.IsCompleted ? request.PlayedAtUtc : null;
            var command = new RoundUpsertCommand(name, request.IsCompleted, playedAtUtc, NormalizeNotes(request.Notes));
            var round = await service.CreateRoundAsync(competitionId, command, ct).ConfigureAwait(false);
            return Results.Created($"/api/rounds/{round.RoundId}", round);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (DbUpdateException)
        {
            return EndpointHelpers.CreateConflictProblem("Já existe uma rodada com os dados informados.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao criar a rodada.");
        }
    }

    private static async Task<IResult> UpdateRoundAsync(
        Guid roundId,
        [FromBody] RoundUpsertRequest request,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            var name = request.Name!.Trim();
            if (name.Length < 3)
            {
                return EndpointHelpers.CreateValidationProblem("O nome da rodada deve conter ao menos 3 caracteres úteis.");
            }

            var playedAtUtc = request.IsCompleted ? request.PlayedAtUtc : null;
            var command = new RoundUpsertCommand(name, request.IsCompleted, playedAtUtc, NormalizeNotes(request.Notes));
            var round = await service.UpdateRoundAsync(roundId, command, ct).ConfigureAwait(false);
            return round is null
                ? EndpointHelpers.CreateNotFoundProblem("Rodada não encontrada.")
                : Results.Ok(round);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return EndpointHelpers.CreateConflictProblem("Já existe uma rodada com os dados informados.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao atualizar a rodada.");
        }
    }

    private static async Task<IResult> DeleteRoundAsync(
        Guid roundId,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        try
        {
            var deleted = await service.DeleteRoundAsync(roundId, ct).ConfigureAwait(false);
            return deleted ? Results.NoContent() : EndpointHelpers.CreateNotFoundProblem("Rodada não encontrada.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao remover a rodada.");
        }
    }

    private static async Task<IResult> CompleteRoundAsync(
        Guid roundId,
        [FromBody] RoundCompletionRequest request,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            var playedAtUtc = request.IsCompleted!.Value ? request.PlayedAtUtc : null;
            var command = new RoundCompletionCommand(request.IsCompleted!.Value, playedAtUtc);
            var round = await service.UpdateRoundCompletionAsync(roundId, command, ct).ConfigureAwait(false);
            return round is null
                ? EndpointHelpers.CreateNotFoundProblem("Rodada não encontrada.")
                : Results.Ok(round);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao atualizar o status da rodada.");
        }
    }

    private static async Task<IResult> UpdateSeasonScheduleAsync(
        Guid seasonId,
        [FromBody] SeasonScheduleUpdateRequest request,
        ISeasonAdminService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        if (!TryValidate(request, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            var items = request.Items
                .OrderBy(i => i.Order)
                .Select(i => new SeasonScheduleUpdateItemDto(i.Order, i.RoundId))
                .ToList();
            var command = new SeasonScheduleUpdateCommand(seasonId, items);
            var schedule = await service.UpdateSeasonScheduleAsync(command, ct).ConfigureAwait(false);
            return Results.Ok(schedule);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return EndpointHelpers.CreateValidationProblem(ex.Message);
        }
        catch (DbUpdateException)
        {
            return EndpointHelpers.CreateConflictProblem("Não foi possível atualizar a ordem do calendário.");
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao atualizar o calendário da temporada.");
        }
    }

    private static async Task<IResult> GetRoundSelectionAsync(Guid roundId, IRoundSelectionService service, CancellationToken ct)
    {
        try
        {
            var selection = await service.GetByRoundAsync(roundId, ct).ConfigureAwait(false);
            if (selection is null)
            {
                return EndpointHelpers.CreateValidationProblem("Rodada inválida.");
            }

            return Results.Ok(selection);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException ex)
        {
            return EndpointHelpers.CreateNotFoundProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao carregar a seleção da rodada.");
        }
    }

    private static async Task<IResult> AddPlayersToSelectionAsync(
        Guid roundId,
        [FromBody] RoundSelectionPlayersRequest? request,
        IRoundSelectionService service,
        CancellationToken ct)
    {
        if (request is null)
        {
            return EndpointHelpers.CreateValidationProblem("Payload inválido.");
        }

        try
        {
            var result = await service.AddPlayersAsync(roundId, request.PlayerIds, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao atualizar a seleção da rodada.");
        }
    }

    private static async Task<IResult> RemovePlayerFromSelectionAsync(
        Guid roundId,
        Guid playerId,
        IRoundSelectionService service,
        CancellationToken ct)
    {
        try
        {
            var result = await service.RemovePlayerAsync(roundId, playerId, ct).ConfigureAwait(false);
            if (!result.Success && string.Equals(result.Message, "Rodada não encontrada.", StringComparison.OrdinalIgnoreCase))
            {
                return EndpointHelpers.CreateNotFoundProblem(result.Message);
            }

            return Results.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message);
        }
        catch (Exception)
        {
            return Results.Problem("Falha ao atualizar a seleção da rodada.");
        }
    }

    private static bool TryValidate(object model, out IResult? errorResult)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model);
        if (Validator.TryValidateObject(model, context, validationResults, true))
        {
            errorResult = null;
            return true;
        }

        var errors = validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty), (result, member) => new { result, member })
            .GroupBy(x => string.IsNullOrWhiteSpace(x.member) ? "_" : x.member, x => x.result.ErrorMessage ?? "Valor inválido.")
            .ToDictionary(g => g.Key, g => g.ToArray());

        errorResult = EndpointHelpers.CreateValidationProblem("Falha na validação dos dados informados.", errors);
        return false;
    }

    private static string? NormalizeNotes(string? notes)
        => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
}
