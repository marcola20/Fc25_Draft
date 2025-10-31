using System.Globalization;
using System.Linq;
using System.Text;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fc25Draft.Web.Endpoints.Market;

public static class MarketHistoryEndpoints
{
    public static IEndpointRouteBuilder MapMarketHistoryEndpoints(this IEndpointRouteBuilder api)
    {
        var history = api.MapGroup("/market/history");
        history.MapGet(string.Empty, HandleQueryAsync).AllowAnonymous();
        history.MapGet("/export", HandleExportAsync).AllowAnonymous();

        var adminHistory = api.MapGroup("/admin/market/history").RequireAuthorization("AdminOnly");
        adminHistory.MapGet(string.Empty, HandleQueryAsync);
        adminHistory.MapGet("/export", HandleExportAsync);

        return api;
    }

    private static async Task<IResult> HandleQueryAsync(
        [AsParameters] MarketHistoryQueryParameters request,
        IMarketHistoryQueryService historyService,
        CancellationToken ct)
    {
        if (!request.TryCreateFilter(out var filter, out var errorResult))
        {
            return errorResult!;
        }

        try
        {
            var result = await historyService.QueryAsync(filter, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> HandleExportAsync(
        [AsParameters] MarketHistoryQueryParameters request,
        IMarketHistoryQueryService historyService,
        CancellationToken ct)
    {
        if (!request.TryCreateFilter(out var filter, out var errorResult))
        {
            return errorResult!;
        }

        var items = await historyService.ExportAsync(filter, ct).ConfigureAwait(false);
        var csv = BuildCsv(items);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var fileName = $"historico-mercado-{timestamp}.csv";
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return Results.File(content, "text/csv", fileName);
    }

    private static string BuildCsv(IReadOnlyList<MarketTransactionDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Data/Hora (UTC);Ciclo;Item;Jogador;Posição;Equipe origem;Equipe destino;Tipo;Valor;Responsável;Notas");

        foreach (var item in items)
        {
            var amount = item.Amount.HasValue
                ? item.Amount.Value.ToString("F2", CultureInfo.InvariantCulture)
                : string.Empty;

            sb
                .Append(Escape(item.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(';')
                .Append(Escape(item.CycleId.ToString())).Append(';')
                .Append(Escape(item.ItemId.ToString())).Append(';')
                .Append(Escape(item.PlayerName)).Append(';')
                .Append(Escape(item.PositionName)).Append(';')
                .Append(Escape(item.TeamName ?? string.Empty)).Append(';')
                .Append(Escape(item.TargetTeamName ?? string.Empty)).Append(';')
                .Append(Escape(item.TypeName)).Append(';')
                .Append(Escape(amount)).Append(';')
                .Append(Escape(item.PerformedBy)).Append(';')
                .Append(Escape(item.Notes ?? string.Empty)).AppendLine();
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var sanitized = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{sanitized}\"" : sanitized;
    }

    private sealed record MarketHistoryQueryParameters
    {
        public Guid? CycleId { get; init; }
        public Guid? ItemId { get; init; }
        public int? PlayerId { get; init; }
        public string? PlayerName { get; init; }
        public string? TeamName { get; init; }
        public string? TargetTeamName { get; init; }
        public int? Type { get; init; }
        public string? PerformedBy { get; init; }
        public DateTime? FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 50;

        public bool TryCreateFilter(out MarketHistoryFilter filter, out IResult? errorResult)
        {
            filter = default!;
            errorResult = null;

            if (Page < 1)
            {
                errorResult = Results.BadRequest(new { message = "Página deve ser maior ou igual a 1." });
                return false;
            }

            if (PageSize < 1)
            {
                errorResult = Results.BadRequest(new { message = "Tamanho da página deve ser maior ou igual a 1." });
                return false;
            }

            if (FromUtc.HasValue && ToUtc.HasValue && FromUtc.Value > ToUtc.Value)
            {
                errorResult = Results.BadRequest(new { message = "A data inicial deve ser menor ou igual à data final." });
                return false;
            }

            MarketTransactionType? type = null;
            if (Type.HasValue)
            {
                if (!Enum.IsDefined(typeof(MarketTransactionType), Type.Value))
                {
                    errorResult = Results.BadRequest(new { message = "Tipo de transação inválido." });
                    return false;
                }

                type = (MarketTransactionType)Type.Value;
            }

            filter = new MarketHistoryFilter
            {
                CycleId = CycleId,
                ItemId = ItemId,
                PlayerId = PlayerId,
                PlayerName = string.IsNullOrWhiteSpace(PlayerName) ? null : PlayerName.Trim(),
                TeamName = string.IsNullOrWhiteSpace(TeamName) ? null : TeamName.Trim(),
                TargetTeamName = string.IsNullOrWhiteSpace(TargetTeamName) ? null : TargetTeamName.Trim(),
                Type = type,
                PerformedBy = string.IsNullOrWhiteSpace(PerformedBy) ? null : PerformedBy.Trim(),
                FromUtc = FromUtc,
                ToUtc = ToUtc,
                Page = Page,
                PageSize = PageSize
            };

            return true;
        }
    }
}
