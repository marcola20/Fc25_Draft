using System.Globalization;
using System.Text.Json;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class AdminLogService : IAdminLogService
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
    private readonly DraftDbContext _db;

    public AdminLogService(DraftDbContext db) => _db = db;

    public async Task<IReadOnlyList<AdminLogItemDto>> ListarAsync(int limite, CancellationToken ct)
    {
        var logs = await _db.AdminActionsLogs.AsNoTracking()
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(limite)
            .ToListAsync(ct);

        // Quem fez: o registro guarda o id do token de admin.
        var admins = await _db.AdminTokens.AsNoTracking()
            .Select(a => new { a.AdminTokenId, Nome = a.Treinador != null ? a.Treinador.Nome : a.Description })
            .ToDictionaryAsync(a => a.AdminTokenId.ToString(), a => a.Nome ?? "Admin", StringComparer.OrdinalIgnoreCase, ct);
        var times = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        // Jogadores citados no payload aparecem pelo PlayerGuid ou, nos registros antigos, pelo PlayerId.
        var guids = logs.SelectMany(l => GuidsDoPayload(l.PayloadJson)).Distinct().ToList();
        var jogadores = await _db.Players.AsNoTracking()
            .Where(p => guids.Contains(p.PlayerGuid))
            .ToDictionaryAsync(p => p.PlayerGuid, p => p.Name, ct);
        var ids = logs.SelectMany(l => IdsDeJogadorDoPayload(l.PayloadJson)).Distinct().ToList();
        var jogadoresPorId = await _db.Players.AsNoTracking()
            .Where(p => ids.Contains(p.PlayerId))
            .ToDictionaryAsync(p => p.PlayerId, p => p.Name, ct);

        string Nome(Guid id) => times.TryGetValue(id, out var t) ? t : jogadores.TryGetValue(id, out var j) ? j : id.ToString()[..8];
        string NomeDoJogador(int id) => jogadoresPorId.GetValueOrDefault(id, $"#{id}");

        return logs.Select(l => new AdminLogItemDto(
                l.CreatedAtUtc.Kind == DateTimeKind.Local ? l.CreatedAtUtc.ToUniversalTime() : l.CreatedAtUtc,
                Acao(l.ActionType),
                admins.GetValueOrDefault(l.PerformedBy, Guid.TryParse(l.PerformedBy, out _) ? "Admin removido" : l.PerformedBy),
                Detalhes(l.PayloadJson, Nome, NomeDoJogador)))
            .ToList();
    }

    private static string Acao(AdminActionType tipo) => tipo switch
    {
        AdminActionType.AdjustBudget => "Ajuste de caixa",
        AdminActionType.CancelMarketItem => "Cancelou item do leilão",
        AdminActionType.SellPlayers => "Venda de jogadores",
        AdminActionType.SwapPlayers => "Troca de jogadores",
        AdminActionType.MovePlayer => "Moveu jogador",
        AdminActionType.ResetMarketItemBids => "Zerou os lances de um item",
        AdminActionType.CancelOffer => "Cancelou proposta",
        _ => tipo.ToString()
    };

    private static string Campo(string chave) => chave.ToLowerInvariant() switch
    {
        "teamid" => "Time",
        "fromteamid" => "De",
        "toteamid" => "Para",
        "teamaid" => "Time A",
        "teambid" => "Time B",
        "playerid" or "playerguid" => "Jogador",
        "playerids" => "Jogadores",
        "playersfroma" => "Jogadores do time A",
        "playersfromb" => "Jogadores do time B",
        "delta" or "amount" or "valor" or "money" => "Valor",
        "cashadjustfromatob" => "Dinheiro de A para B",
        "reason" or "motivo" => "Motivo",
        "itemid" => "Item do leilão",
        "offerid" => "Proposta",
        "removedbids" => "Lances removidos",
        "previousleaderteamid" => "Quem liderava",
        "releasedamount" => "Valor liberado",
        _ => chave
    };

    private static bool EhDinheiro(string chave) =>
        chave.ToLowerInvariant() is "delta" or "amount" or "valor" or "money" or "cashadjustfromatob" or "releasedamount";

    private static bool EhJogador(string chave) => chave.ToLowerInvariant() is "playerid" or "playerids";

    private static IReadOnlyList<AdminLogDetalheDto> Detalhes(string json, Func<Guid, string> nome, Func<int, string> jogador)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return [new("Dados", json)];

            return doc.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                .Select(p => new AdminLogDetalheDto(Campo(p.Name), Valor(p.Name, p.Value, nome, jogador)))
                .Where(d => d.Valor.Length > 0)
                .ToList();
        }
        catch (JsonException)
        {
            return [new("Dados", json)];
        }
    }

    private static string Valor(string chave, JsonElement v, Func<Guid, string> nome, Func<int, string> jogador) => v.ValueKind switch
    {
        JsonValueKind.String when Guid.TryParse(v.GetString(), out var g) => nome(g),
        JsonValueKind.String when EhDinheiro(chave) && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d.ToString("C0", Br),
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number when EhDinheiro(chave) && v.TryGetDecimal(out var d) => d.ToString("C0", Br),
        JsonValueKind.Number when EhJogador(chave) && v.TryGetInt32(out var id) => jogador(id),
        JsonValueKind.Array => string.Join(", ", v.EnumerateArray().Select(e => Valor(chave, e, nome, jogador))),
        _ => v.ToString()
    };

    private static IEnumerable<int> IdsDeJogadorDoPayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return [];
            return doc.RootElement.EnumerateObject()
                .Where(p => EhJogador(p.Name))
                .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array ? p.Value.EnumerateArray().ToList() : [p.Value])
                .Where(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out _))
                .Select(e => e.GetInt32())
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IEnumerable<Guid> GuidsDoPayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) yield break;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); } catch (JsonException) { yield break; }
        using (doc)
        {
            var pilha = new Stack<JsonElement>([doc.RootElement]);
            while (pilha.Count > 0)
            {
                var e = pilha.Pop();
                switch (e.ValueKind)
                {
                    case JsonValueKind.Object: foreach (var p in e.EnumerateObject()) pilha.Push(p.Value); break;
                    case JsonValueKind.Array: foreach (var i in e.EnumerateArray()) pilha.Push(i); break;
                    case JsonValueKind.String when Guid.TryParse(e.GetString(), out var g): yield return g; break;
                }
            }
        }
    }
}
