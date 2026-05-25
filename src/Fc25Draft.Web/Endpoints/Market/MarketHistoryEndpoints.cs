using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Web.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Fc25Draft.Web.Endpoints.Market;

public static class MarketHistoryEndpoints
{
    public static IEndpointRouteBuilder MapMarketHistoryEndpoints(this IEndpointRouteBuilder api)
    {
        var history = api.MapGroup("/market/history");
        history.MapGet(string.Empty, HandleQueryAsync).AllowAnonymous();
        history.MapGet("/export", HandleExportAsync).AllowAnonymous();

        api.MapGet("/market/items/{itemId:guid}/history", HandleItemHistoryAsync).AllowAnonymous();
        api.MapGet("/market/cycles/{cycleId:guid}/history", HandleCycleHistoryAsync).AllowAnonymous();

        var adminHistory = api.MapGroup("/admin/market/history").RequireAuthorization("AdminOnly");
        adminHistory.MapGet(string.Empty, HandleQueryAsync);
        adminHistory.MapGet("/export", HandleExportAsync);

        return api;
    }

    private static Task<IResult> HandleQueryAsync(
        [AsParameters] MarketHistoryQueryParameters request,
        IMarketHistoryQueryService historyService,
        CancellationToken ct)
        => ExecuteQueryAsync(request, historyService, ct);

    private static Task<IResult> HandleItemHistoryAsync(
        Guid itemId,
        [AsParameters] MarketHistoryQueryParameters request,
        IMarketHistoryQueryService historyService,
        CancellationToken ct)
    {
        if (itemId == Guid.Empty)
        {
            return Task.FromResult<IResult>(Results.BadRequest(new { message = "Identificador do item é obrigatório." }));
        }

        return ExecuteQueryAsync(request, historyService, ct, itemIdOverride: itemId);
    }

    private static Task<IResult> HandleCycleHistoryAsync(
        Guid cycleId,
        [AsParameters] MarketHistoryQueryParameters request,
        IMarketHistoryQueryService historyService,
        CancellationToken ct)
    {
        if (cycleId == Guid.Empty)
        {
            return Task.FromResult<IResult>(Results.BadRequest(new { message = "Identificador do ciclo é obrigatório." }));
        }

        return ExecuteQueryAsync(request, historyService, ct, cycleIdOverride: cycleId);
    }

    private static async Task<IResult> ExecuteQueryAsync(
        MarketHistoryQueryParameters request,
        IMarketHistoryQueryService historyService,
        CancellationToken ct,
        Guid? cycleIdOverride = null,
        Guid? itemIdOverride = null)
    {
        if (!request.TryCreateFilter(out var filter, out var errorResult, cycleIdOverride, itemIdOverride))
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

        try
        {
            var items = await historyService.ExportAsync(filter, ct).ConfigureAwait(false);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            var fileName = $"historico-mercado-{timestamp}.csv";

            return Results.Stream(
                async stream =>
                {
                    await WriteCsvAsync(stream, items, ct).ConfigureAwait(false);
                },
                "text/csv",
                fileName
            );
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task WriteCsvAsync(Stream stream, IReadOnlyList<MarketTransactionDto> items, CancellationToken ct)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        await using var writer = new StreamWriter(stream, encoding, bufferSize: 1024, leaveOpen: true);

        await writer.WriteLineAsync("DataHoraUtc;Evento;Jogador;TimeOrigem;TimeDestino;Valor;Observacoes").ConfigureAwait(false);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            var amount = item.Amount.HasValue
                ? string.Format(BrazilTime.Culture, "{0:C}", item.Amount.Value)
                : string.Empty;
            var observation = MarketHistoryTextFormatter.FormatObservation(item);

            var lineBuilder = new StringBuilder();
            lineBuilder
                .Append(Escape(item.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(';')
                .Append(Escape(item.TypeName)).Append(';')
                .Append(Escape(item.PlayerName)).Append(';')
                .Append(Escape(item.TeamName ?? string.Empty)).Append(';')
                .Append(Escape(item.TargetTeamName ?? string.Empty)).Append(';')
                .Append(Escape(amount)).Append(';')
                .Append(Escape(observation));

            await writer.WriteLineAsync(lineBuilder.ToString()).ConfigureAwait(false);
        }

        await writer.FlushAsync().ConfigureAwait(false);
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
        [FromQuery(Name = "cycleId")] public string? CycleIdRaw { get; init; }
        [FromQuery(Name = "itemId")] public string? ItemIdRaw { get; init; }
        [FromQuery(Name = "teamId")] public string? TeamIdRaw { get; init; }
        public int? PlayerId { get; init; }
        public string? PlayerName { get; init; }
        public string? TeamName { get; init; }
        public string? TargetTeamName { get; init; }
        [FromQuery(Name = "type")] public string[] TypeRaw { get; init; } = Array.Empty<string>();
        public string? PerformedBy { get; init; }
        [FromQuery(Name = "from")] public string? FromUtcRaw { get; init; }
        [FromQuery(Name = "fromUtc")] public string? LegacyFromUtcRaw { get; init; }
        [FromQuery(Name = "to")] public string? ToUtcRaw { get; init; }
        [FromQuery(Name = "toUtc")] public string? LegacyToUtcRaw { get; init; }
        public bool OnlyAcquisitions { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 50;

        public bool TryCreateFilter(out MarketHistoryFilter filter, out IResult? errorResult, Guid? cycleIdOverride = null, Guid? itemIdOverride = null)
        {
            filter = default!;
            errorResult = null;

            if (!TryParseGuid(CycleIdRaw, "cycleId", out var cycleId, out errorResult) ||
                !TryParseGuid(ItemIdRaw, "itemId", out var itemId, out errorResult) ||
                !TryParseGuid(TeamIdRaw, "teamId", out var teamId, out errorResult))
            {
                return false;
            }

            if (!TryParseDateTime(FromUtcRaw, "from", out var fromUtc, out errorResult) ||
                !TryParseDateTime(LegacyFromUtcRaw, "fromUtc", out var legacyFromUtc, out errorResult) ||
                !TryParseDateTime(ToUtcRaw, "to", out var toUtc, out errorResult) ||
                !TryParseDateTime(LegacyToUtcRaw, "toUtc", out var legacyToUtc, out errorResult))
            {
                return false;
            }

            var parsedType = default(int?);
            var sanitizedTypeValues = TypeRaw
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .ToList();

            if (sanitizedTypeValues.Count > 1)
            {
                errorResult = Results.BadRequest(new { message = "Informe apenas um valor para o parâmetro \"type\"." });
                return false;
            }

            if (sanitizedTypeValues.Count == 1)
            {
                var rawType = sanitizedTypeValues[0];

                if (rawType.Contains(',', StringComparison.Ordinal))
                {
                    errorResult = Results.BadRequest(new { message = "O parâmetro \"type\" não aceita múltiplos valores." });
                    return false;
                }

                if (!int.TryParse(rawType, NumberStyles.Integer, CultureInfo.InvariantCulture, out var typeValue))
                {
                    errorResult = Results.BadRequest(new { message = "Parâmetro \"type\" deve ser um número inteiro válido." });
                    return false;
                }

                parsedType = typeValue;
            }

            var effectiveFromUtc = fromUtc ?? legacyFromUtc;
            var effectiveToUtc = toUtc ?? legacyToUtc;

            if (effectiveFromUtc.HasValue && effectiveToUtc.HasValue && effectiveFromUtc.Value > effectiveToUtc.Value)
            {
                errorResult = Results.BadRequest(new { message = "A data inicial deve ser menor ou igual à data final." });
                return false;
            }

            var sanitizedPage = Page < 1 ? 1 : Page;
            var sanitizedPageSize = PageSize < 1 ? 1 : Math.Min(PageSize, 100);

            MarketTransactionType? type = null;
            if (parsedType.HasValue)
            {
                if (!Enum.IsDefined(typeof(MarketTransactionType), parsedType.Value))
                {
                    errorResult = Results.BadRequest(new { message = "Tipo de transação inválido." });
                    return false;
                }

                type = (MarketTransactionType)parsedType.Value;
            }

            var normalizedCycleId = cycleIdOverride ?? cycleId;
            var normalizedItemId = itemIdOverride ?? itemId;
            var normalizedTeamId = teamId;

            filter = new MarketHistoryFilter
            {
                CycleId = NormalizeGuid(normalizedCycleId),
                ItemId = NormalizeGuid(normalizedItemId),
                TeamId = NormalizeGuid(normalizedTeamId),
                PlayerId = PlayerId,
                PlayerName = string.IsNullOrWhiteSpace(PlayerName) ? null : PlayerName.Trim(),
                TeamName = string.IsNullOrWhiteSpace(TeamName) ? null : TeamName.Trim(),
                TargetTeamName = string.IsNullOrWhiteSpace(TargetTeamName) ? null : TargetTeamName.Trim(),
                Type = type,
                OnlyAcquisitions = OnlyAcquisitions,
                PerformedBy = string.IsNullOrWhiteSpace(PerformedBy) ? null : PerformedBy.Trim(),
                FromUtc = effectiveFromUtc,
                ToUtc = effectiveToUtc,
                Page = sanitizedPage,
                PageSize = sanitizedPageSize
            };

            return true;
        }

        private static bool TryParseGuid(string? value, string parameterName, out Guid? result, out IResult? errorResult)
        {
            result = null;
            errorResult = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!Guid.TryParse(value.Trim(), out var parsed))
            {
                errorResult = Results.BadRequest(new { message = $"Parâmetro \"{parameterName}\" deve ser um GUID válido." });
                return false;
            }

            result = parsed;
            return true;
        }

        private static bool TryParseDateTime(string? value, string parameterName, out DateTime? result, out IResult? errorResult)
        {
            result = null;
            errorResult = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                errorResult = Results.BadRequest(new
                {
                    message = $"Parâmetro \"{parameterName}\" possui formato de data/hora inválido. Utilize o padrão ISO 8601 (ex.: 2024-01-31T12:00:00Z)."
                });
                return false;
            }

            result = parsed.UtcDateTime;
            return true;
        }

        private static Guid? NormalizeGuid(Guid? value)
            => value.HasValue && value.Value == Guid.Empty ? null : value;
    }
}
