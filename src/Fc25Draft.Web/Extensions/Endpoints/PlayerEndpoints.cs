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
                int page = 1,
                int pageSize = 10,
                CancellationToken ct = default) =>
            {
                var currentPage = Math.Max(1, page);
                var currentPageSize = pageSize <= 0 ? 10 : pageSize;

                var query = db.Players.AsNoTracking().Where(p => true);

                if (overallMin.HasValue && overallMax.HasValue && overallMin > overallMax)
                    return Results.BadRequest(new { message = "Overall mínimo não pode ser maior que o máximo." });

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var pattern = $"%{q.Trim()}%";
                    // ILIKE + UNACCENT (extensão PostgreSQL)
                    query = query.Where(p => EF.Functions.ILike(
                        EF.Functions.Unaccent(p.Name),
                        EF.Functions.Unaccent(pattern)));
                }

                if (pos is { Length: > 0 })
                {
                    var positions = pos.Distinct().ToArray();
                    query = query.Where(p => positions.Contains(p.PositionId));
                }

                if (onlyAvailable is true)
                    query = query.Where(p => !p.TeamRosters.Any());

                if (overallMin.HasValue)
                    query = query.Where(p => p.Overall >= overallMin.Value);

                if (overallMax.HasValue)
                    query = query.Where(p => p.Overall <= overallMax.Value);

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

                orderedQuery = orderedQuery.ThenBy(p => p.Name);

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
                        p.TeamRosters.Any() ? "Escolhido" : "Disponível",
                        p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault()))
                    .ToListAsync(ct);

                return Results.Ok(new PagedResult<PlayerListItemDto>(items, total));
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
                        p.TeamRosters.Any() ? "Escolhido" : "Disponível",
                        p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault(),
                        p.TeamRosters.Select(r => (Guid?)r.TeamId).FirstOrDefault()))
                    .FirstOrDefaultAsync(ct);

                return player is null ? Results.NotFound() : Results.Ok(player);
            });

            playersApi.MapGet("/export/csv", async (DraftDbContext db, CancellationToken ct) =>
            {
                var players = await LoadPlayerExportAsync(db, ct);
                var csv = BuildPlayerCsv(players);
                return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "jogadores.csv");
            });

            playersApi.MapGet("/export/json", async (DraftDbContext db, CancellationToken ct) =>
            {
                var players = await LoadPlayerExportAsync(db, ct);
                var json = JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true });
                return Results.File(Encoding.UTF8.GetBytes(json), "application/json", "jogadores.json");
            });

            playersApi.MapGet("/export/xlsx", async (DraftDbContext db, CancellationToken ct) =>
            {
                var players = await LoadPlayerExportAsync(db, ct);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Jogadores");
                worksheet.Cell(1, 1).Value = "Nome";
                worksheet.Cell(1, 2).Value = "Posição";
                worksheet.Cell(1, 3).Value = "Overall";
                worksheet.Cell(1, 4).Value = "Status";
                worksheet.Cell(1, 5).Value = "Time";

                for (var i = 0; i < players.Count; i++)
                {
                    var row = i + 2;
                    var p = players[i];
                    worksheet.Cell(row, 1).Value = p.Nome;
                    worksheet.Cell(row, 2).Value = p.Posicao;
                    worksheet.Cell(row, 3).Value = p.Overall;
                    worksheet.Cell(row, 4).Value = p.Status;
                    worksheet.Cell(row, 5).Value = p.Time ?? string.Empty;
                }

                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var bytes = stream.ToArray();
                return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "jogadores.xlsx");
            });

            var adminPlayersApi = routes.MapGroup("/admin/players").RequireAuthorization("AdminOnly");

            adminPlayersApi.MapPost(string.Empty, async (IPlayerService playerService, PlayerCreateDto dto) =>
            {
                try
                {
                    var id = await playerService.CreateAsync(dto);
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

            adminPlayersApi.MapPost("/import", async (HttpRequest request, IPlayerService playerService, CancellationToken ct) =>
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
                return Results.Ok(result);
            });

            return routes;
        }

        #region Helper
        private static async Task<List<PlayerExportDto>> LoadPlayerExportAsync(DraftDbContext db, CancellationToken ct)
        {
            var players = await db.Players
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new PlayerExportDto(
                    p.Name,
                    p.Position.Name,
                    p.Overall,
                    p.TeamRosters.Any() ? "Escolhido" : "Disponível",
                    p.TeamRosters.Select(r => r.Team.TeamName).FirstOrDefault()
                ))
                .ToListAsync(ct);

            return players;
        }


        private static string BuildPlayerCsv(List<PlayerExportDto> players)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Nome;Posição;Overall;Status;Time");

            foreach (var p in players)
            {
                sb.AppendLine(string.Join(";",
                    Csv(p.Nome),
                    Csv(p.Posicao),
                    p.Overall.ToString(CultureInfo.InvariantCulture),
                    Csv(p.Status),
                    Csv(p.Time ?? string.Empty)));
            }

            return sb.ToString();

            static string Csv(string value)
            {
                if (value.Contains('"') || value.Contains(';') || value.Contains('\n') || value.Contains('\r'))
                    return $"\"{value.Replace("\"", "\"\"")}\"";
                return value;
            }
        }

        private sealed record PlayerExportDto(string Nome, string Posicao, int Overall, string Status, string? Time);
        #endregion
    }
}
