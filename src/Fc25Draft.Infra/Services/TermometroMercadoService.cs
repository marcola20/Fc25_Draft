using System.Globalization;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class TermometroMercadoService : ITermometroMercadoService
{
    // Ciclos que começam até esse tempo depois do fim do anterior ficam na mesma janela.
    private static readonly TimeSpan IntervaloMaximoNaJanela = TimeSpan.FromDays(3);

    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly DraftDbContext _db;

    public TermometroMercadoService(DraftDbContext db) => _db = db;

    public async Task<IReadOnlyList<TermometroJanelaDto>> ListarJanelasAsync(CancellationToken ct) =>
        (await MontarJanelasAsync(ct))
            .Select(j => j.Dto)
            .OrderByDescending(j => j.Inicio)
            .ToList();

    public async Task<TermometroMercadoDto> CalcularAsync(int? janela, CancellationToken ct)
    {
        var nomes = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        var escolhida = janela is int numero
            ? (await MontarJanelasAsync(ct)).FirstOrDefault(j => j.Dto.Numero == numero)
            : null;
        var cicloIds = escolhida?.CicloIds;

        // Itens que chegaram ao mercado (rascunhos e cancelados não contam).
        var itensQuery = _db.MarketItems.AsNoTracking()
            .Where(i => i.Status != MarketItemStatus.Draft && i.Status != MarketItemStatus.Canceled);
        if (cicloIds is not null)
            itensQuery = itensQuery.Where(i => cicloIds.Contains(i.CycleId));

        var itens = await itensQuery
            .Select(i => new TermometroItemInput(
                i.ItemId, i.PlayerId, i.Player.Name, i.Player.Position.Name, i.Player.Overall,
                i.BasePrice, i.BuyNowPrice, i.Status == MarketItemStatus.Sold, i.WinnerTeamId, i.CurrentLeaderAmount))
            .ToListAsync(ct);

        var itemIds = itens.Select(i => i.ItemId).ToList();
        var lances = await _db.MarketBids.AsNoTracking()
            .Where(b => itemIds.Contains(b.ItemId))
            .Select(b => new TermometroLanceInput(b.ItemId, b.TeamId, b.Amount))
            .ToListAsync(ct);

        // Leilões pelo ciclo; vendas entre times e vendas rápidas pela data da janela.
        var tipos = new List<TransferType> { TransferType.MarketAuction, TransferType.TeamSale, TransferType.QuickSell };
        var transfQuery = _db.TransferHistories.AsNoTracking()
            .Where(t => tipos.Contains(t.Type) && t.Amount != null);
        if (escolhida is not null)
        {
            var (inicio, fim) = (escolhida.Dto.Inicio, escolhida.Dto.Fim);
            transfQuery = transfQuery.Where(t =>
                t.Type == TransferType.MarketAuction
                    ? t.CycleId != null && cicloIds!.Contains(t.CycleId.Value)
                    : t.PerformedAtUtc >= inicio && t.PerformedAtUtc <= fim);
        }

        var transferencias = await transfQuery
            .Select(t => new TermometroTransferenciaInput(
                t.PerformedAtUtc, t.Type, t.PlayerId, t.Player.Name, t.Player.Position.Name,
                t.FromTeamId, t.ToTeamId, t.Amount!.Value))
            .ToListAsync(ct);

        return TermometroMercado.Calcular(itens, lances, transferencias, nomes);
    }

    private sealed record Janela(TermometroJanelaDto Dto, List<Guid> CicloIds);

    private async Task<List<Janela>> MontarJanelasAsync(CancellationToken ct)
    {
        var ciclos = await _db.MarketCycles.AsNoTracking()
            .Where(c => c.Status != MarketCycleStatus.Draft)
            .OrderBy(c => c.StartsAtUtc)
            .Select(c => new { c.CycleId, c.StartsAtUtc, c.EndsAtUtc })
            .ToListAsync(ct);

        var grupos = new List<(DateTime Inicio, DateTime Fim, List<Guid> Ids)>();
        foreach (var c in ciclos)
        {
            if (grupos.Count > 0 && c.StartsAtUtc <= grupos[^1].Fim + IntervaloMaximoNaJanela)
            {
                var g = grupos[^1];
                g.Ids.Add(c.CycleId);
                grupos[^1] = (g.Inicio, c.EndsAtUtc > g.Fim ? c.EndsAtUtc : g.Fim, g.Ids);
            }
            else
            {
                grupos.Add((c.StartsAtUtc, c.EndsAtUtc, new List<Guid> { c.CycleId }));
            }
        }

        // "Janela de abr/2026"; se o mesmo mês tiver duas, a segunda ganha número.
        var usados = new Dictionary<string, int>();
        return grupos
            .Select((g, i) =>
            {
                var mes = g.Inicio.ToString("MMM/yyyy", Brasil).Replace(".", "");
                usados[mes] = usados.GetValueOrDefault(mes) + 1;
                var nome = usados[mes] == 1 ? $"Janela de {mes}" : $"Janela de {mes} ({usados[mes]})";
                return new Janela(new TermometroJanelaDto(i + 1, nome, g.Inicio, g.Fim, g.Ids.Count), g.Ids);
            })
            .ToList();
    }
}
