using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class PlayerEndpoints
    {
        public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder routes)
        {
            var playersApi = routes.MapGroup("/players");

            playersApi.MapGet(string.Empty, async (
                DraftDbContext db,
                string? q,
                [FromQuery(Name = "pos")] short[]? pos,
                bool? onlyAvailable,
                int? overallMin,
                int? overallMax,
                string? sortBy,
                string? sortOrder,
                int? ageMin,
                int? ageMax,
                int page = 1,
                int pageSize = 10,
                CancellationToken ct = default) =>
            {
                var currentPage = page < 1 ? 1 : page;
                var currentPageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 100);

                if (overallMin.HasValue && overallMax.HasValue && overallMin > overallMax)
                    return Results.BadRequest(new { message = "Overall mínimo não pode ser maior que o máximo." });
                if (ageMin.HasValue && ageMax.HasValue && ageMin > ageMax)
                    return Results.BadRequest(new { message = "Idade mínima não pode ser maior que a máxima." });

                var query = BuildFilteredQuery(db, q, pos, onlyAvailable, overallMin, overallMax, ageMin, ageMax);
                var orderedQuery = ApplySort(query, sortBy, sortOrder);

                var total = await query.CountAsync(ct);

                var items = await orderedQuery
                    .Skip((currentPage - 1) * currentPageSize)
                    .Take(currentPageSize)
                    .Select(p => new PlayerListItemDto(
                        p.PlayerId,
                        p.Name,
                        p.PositionId,
                        p.Position.Name,
                        p.Overall,
                        p.Age,
                        p.TeamRosters.Any() ? "Escolhido" : p.AposentadoNaTemporada != null ? "Aposentado" : "Disponível",
                        p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                        p.Pais))
                    .ToListAsync(ct);

                return Results.Ok(new PagedResult<PlayerListItemDto>(items, total, currentPage, currentPageSize));
            });

            playersApi.MapGet("/{id:int}", async (DraftDbContext db, int id, CancellationToken ct = default) =>
            {
                var player = await db.Players
                    .AsNoTracking()
                    .Where(p => p.PlayerId == id)
                    .Select(p => new PlayerDetailsDto(
                        p.PlayerId,
                        p.Name,
                        p.PositionId,
                        p.Position.Name,
                        p.Overall,
                        p.Age,
                        p.TeamRosters.Any() ? "Escolhido" : p.AposentadoNaTemporada != null ? "Aposentado" : "Disponível",
                        p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                        p.TeamRosters.Select(r => (Guid?)r.TeamId).FirstOrDefault(),
                        p.Atributos == null ? null : AtributosPes.ParaDto(p.Atributos),
                        p.DespedidaAnunciadaEm != null || p.AposentadoNaTemporada != null ? p.UltimaTemporada : null,
                        p.AposentadoNaTemporada,
                        p.Pais))
                    .FirstOrDefaultAsync(ct);

                return player is null ? Results.NotFound() : Results.Ok(player);
            });

            playersApi.MapGet("/export/csv", async (
                DraftDbContext db,
                string? q,
                [FromQuery(Name = "pos")] short[]? pos,
                bool? onlyAvailable,
                int? overallMin,
                int? overallMax,
                string? sortBy,
                string? sortOrder,
                int? ageMin,
                int? ageMax,
                CancellationToken ct) =>
            {
                var players = await LoadPlayerExportAsync(db, q, pos, onlyAvailable, overallMin, overallMax, ageMin, ageMax, sortBy, sortOrder, ct);
                var csv = BuildPlayerCsv(players);
                return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "jogadores.csv");
            });

            playersApi.MapGet("/export/json", async (
                DraftDbContext db,
                string? q,
                [FromQuery(Name = "pos")] short[]? pos,
                bool? onlyAvailable,
                int? overallMin,
                int? overallMax,
                string? sortBy,
                string? sortOrder,
                int? ageMin,
                int? ageMax,
                CancellationToken ct) =>
            {
                var players = await LoadPlayerExportAsync(db, q, pos, onlyAvailable, overallMin, overallMax, ageMin, ageMax, sortBy, sortOrder, ct);
                var json = JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true });
                return Results.File(Encoding.UTF8.GetBytes(json), "application/json", "jogadores.json");
            });

            playersApi.MapGet("/export/xlsx", async (
                DraftDbContext db,
                string? q,
                [FromQuery(Name = "pos")] short[]? pos,
                bool? onlyAvailable,
                int? overallMin,
                int? overallMax,
                string? sortBy,
                string? sortOrder,
                int? ageMin,
                int? ageMax,
                CancellationToken ct) =>
            {
                var players = await LoadPlayerExportAsync(db, q, pos, onlyAvailable, overallMin, overallMax, ageMin, ageMax, sortBy, sortOrder, ct);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Jogadores");
                worksheet.Cell(1, 1).Value = "Nome";
                worksheet.Cell(1, 2).Value = "Posição";
                worksheet.Cell(1, 3).Value = "Overall";
                worksheet.Cell(1, 4).Value = "Idade";
                worksheet.Cell(1, 5).Value = "Status";
                worksheet.Cell(1, 6).Value = "Time";
                worksheet.Cell(1, 7).Value = "País";

                for (var i = 0; i < players.Count; i++)
                {
                    var row = i + 2;
                    var p = players[i];
                    worksheet.Cell(row, 1).Value = p.Nome;
                    worksheet.Cell(row, 2).Value = p.Posicao;
                    worksheet.Cell(row, 3).Value = p.Overall;
                    if (p.Idade.HasValue)
                        worksheet.Cell(row, 4).Value = p.Idade.Value;
                    worksheet.Cell(row, 5).Value = p.Status;
                    worksheet.Cell(row, 6).Value = p.Time ?? string.Empty;
                    worksheet.Cell(row, 7).Value = p.Pais ?? string.Empty;
                }

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var bytes = stream.ToArray();
                return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "jogadores.xlsx");
            });

            var adminPlayersApi = routes.MapGroup("/admin/players").RequireAuthorization("AdminOnly");

            adminPlayersApi.MapPost(string.Empty, async (IPlayerService playerService, IBasePesService basePes, PlayerCreateDto dto) =>
            {
                try
                {
                    var id = await playerService.CreateAsync(dto);
                    await basePes.PreencherFaltantesAsync();
                    return Results.Created($"/api/players/{id}", new { id });
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            });

            adminPlayersApi.MapPut("/{id:int}", async (IPlayerService playerService, int id, PlayerUpdateDto dto) =>
            {
                try
                {
                    await playerService.UpdateAsync(id, dto);
                    return Results.NoContent();
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
            });

            adminPlayersApi.MapPut("/{id:int}/atributos", async (IPlayerService playerService, int id, PlayerAtributosDto dto) =>
            {
                try
                {
                    await playerService.SalvarAtributosAsync(id, dto);
                    return Results.NoContent();
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
            });

            adminPlayersApi.MapDelete("/{id:int}", async (IPlayerService playerService, int id) =>
            {
                try
                {
                    await playerService.DeleteAsync(id);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            });

            adminPlayersApi.MapGet("/pes", (IBasePesService basePes, string? q) =>
                Results.Ok(basePes.Buscar(q ?? string.Empty)));

            adminPlayersApi.MapPost("/import", async (HttpRequest request, IPlayerService playerService, IBasePesService basePes, CancellationToken ct) =>
            {
                if (!request.HasFormContentType)
                    return Results.BadRequest(new { message = "Envie um arquivo CSV válido." });

                var form = await request.ReadFormAsync(ct);
                var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

                if (file is null || file.Length == 0)
                    return Results.BadRequest(new { message = "Arquivo CSV não encontrado." });

                const long maxCsvSize = 5 * 1024 * 1024;
                if (file.Length > maxCsvSize)
                    return Results.BadRequest(new { message = "O arquivo deve ter no máximo 5 MB." });

                await using var stream = file.OpenReadStream();
                var result = await playerService.ImportCsvAsync(stream, ct);
                await basePes.PreencherFaltantesAsync(ct);
                return Results.Ok(result);
            });

            return routes;
        }

        #region Helper
        private static IQueryable<Player> BuildFilteredQuery(
            DraftDbContext db,
            string? q,
            short[]? pos,
            bool? onlyAvailable,
            int? overallMin,
            int? overallMax,
            int? ageMin = null,
            int? ageMax = null)
        {
            var query = db.Players.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                query = query.Where(p => EF.Functions.ILike(
                    EF.Functions.Unaccent(p.Name),
                    EF.Functions.Unaccent(pattern)));
            }

            if (pos?.Length > 0)
            {
                var positions = pos.Distinct().ToList();
                query = query.Where(p => positions.Contains(p.PositionId));
            }

            if (onlyAvailable is true)
                query = query.Where(p => !p.TeamRosters.Any() && p.AposentadoNaTemporada == null);

            if (overallMin.HasValue)
                query = query.Where(p => p.Overall >= overallMin.Value);

            if (overallMax.HasValue)
                query = query.Where(p => p.Overall <= overallMax.Value);

            // Sem idade cadastrada fica de fora quando há filtro de idade.
            if (ageMin.HasValue)
                query = query.Where(p => p.Age != null && p.Age >= ageMin.Value);

            if (ageMax.HasValue)
                query = query.Where(p => p.Age != null && p.Age <= ageMax.Value);

            return query;
        }

        private static IOrderedQueryable<Player> ApplySort(IQueryable<Player> query, string? sortBy, string? sortOrder)
        {
            var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? "overall" : sortBy.Trim().ToLowerInvariant();
            var normalizedSortOrder = string.IsNullOrWhiteSpace(sortOrder) ? "desc" : sortOrder.Trim().ToLowerInvariant();
            var sortDescending = normalizedSortOrder != "asc";

            IOrderedQueryable<Player> orderedQuery = normalizedSortBy switch
            {
                "age" when sortDescending => query.OrderByDescending(p => p.Age ?? int.MinValue),
                "age" => query.OrderBy(p => p.Age ?? int.MaxValue),
                "overall" when sortDescending => query.OrderByDescending(p => p.Overall),
                "overall" => query.OrderBy(p => p.Overall),
                _ when sortDescending => query.OrderByDescending(p => p.Overall),
                _ => query.OrderBy(p => p.Overall)
            };

            return orderedQuery.ThenBy(p => p.Name);
        }

        private static async Task<List<PlayerExportDto>> LoadPlayerExportAsync(
            DraftDbContext db,
            string? q,
            short[]? pos,
            bool? onlyAvailable,
            int? overallMin,
            int? overallMax,
            int? ageMin,
            int? ageMax,
            string? sortBy,
            string? sortOrder,
            CancellationToken ct)
        {
            var query = BuildFilteredQuery(db, q, pos, onlyAvailable, overallMin, overallMax, ageMin, ageMax);
            var orderedQuery = ApplySort(query, sortBy, sortOrder);

            return await orderedQuery
                .Select(p => new PlayerExportDto(
                    p.Name,
                    p.Position.Name,
                    p.Overall,
                    p.Age,
                    p.TeamRosters.Any() ? "Escolhido" : p.AposentadoNaTemporada != null ? "Aposentado" : "Disponível",
                    p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                    p.Pais
                ))
                .ToListAsync(ct);
        }

        private static string BuildPlayerCsv(List<PlayerExportDto> players)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Nome;Posição;Overall;Idade;Status;Time;País");

            foreach (var p in players)
            {
                sb.AppendLine(string.Join(";",
                    Csv(p.Nome),
                    Csv(p.Posicao),
                    p.Overall.ToString(CultureInfo.InvariantCulture),
                    p.Idade?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    Csv(p.Status),
                    Csv(p.Time ?? string.Empty),
                    Csv(p.Pais ?? string.Empty)));
            }

            return sb.ToString();

            static string Csv(string value)
            {
                if (value.Contains('"') || value.Contains(';') || value.Contains('\n') || value.Contains('\r'))
                    return $"\"{value.Replace("\"", "\"\"")}\"";
                return value;
            }
        }

        private sealed record PlayerExportDto(string Nome, string Posicao, int Overall, int? Idade, string Status, string? Time, string? Pais);
        #endregion
    }
}
