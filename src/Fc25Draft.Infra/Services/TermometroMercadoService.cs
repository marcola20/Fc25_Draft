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

    public async Task<IReadOnlyList<TermometroChegadasSaidasDto>> ChegadasESaidasAsync(int? janela, CancellationToken ct)
    {
        var nomes = await _db.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

        // Mesmo recorte do termômetro: leilão pelo ciclo da janela, o resto pela data.
        var query = _db.TransferHistories.AsNoTracking().Where(t => t.Type != TransferType.None);
        if (janela is int numero)
        {
            var escolhida = (await MontarJanelasAsync(ct)).FirstOrDefault(j => j.Dto.Numero == numero);
            if (escolhida is null) return Array.Empty<TermometroChegadasSaidasDto>();
            var cicloIds = escolhida.CicloIds;
            var (inicio, fim) = (escolhida.Dto.Inicio, escolhida.Dto.Fim);
            query = query.Where(t =>
                t.Type == TransferType.MarketAuction
                    ? t.CycleId != null && cicloIds.Contains(t.CycleId.Value)
                    : t.PerformedAtUtc >= inicio && t.PerformedAtUtc <= fim);
        }

        var movimentos = await query
            .Select(t => new
            {
                t.PerformedAtUtc, t.Type, t.PlayerId, t.Player.Name, Posicao = t.Player.Position.Name,
                t.Player.Overall, t.FromTeamId, t.ToTeamId, t.Amount
            })
            .ToListAsync(ct);

        var porTime = new Dictionary<Guid, (List<TermometroMovimentoDto> Chegadas, List<TermometroMovimentoDto> Saidas)>();
        (List<TermometroMovimentoDto> Chegadas, List<TermometroMovimentoDto> Saidas) Do(Guid time)
        {
            if (!porTime.TryGetValue(time, out var listas))
                porTime[time] = listas = (new List<TermometroMovimentoDto>(), new List<TermometroMovimentoDto>());
            return listas;
        }

        foreach (var m in movimentos)
        {
            if (m.ToTeamId is Guid para)
                Do(para).Chegadas.Add(new TermometroMovimentoDto(
                    m.PerformedAtUtc, RotuloDoTipo(m.Type, chegando: true), m.PlayerId, m.Name, m.Posicao, m.Overall,
                    m.FromTeamId, m.FromTeamId is Guid de ? nomes.GetValueOrDefault(de) : null, m.Amount));
            if (m.FromTeamId is Guid de2)
                Do(de2).Saidas.Add(new TermometroMovimentoDto(
                    m.PerformedAtUtc, RotuloDoTipo(m.Type, chegando: false), m.PlayerId, m.Name, m.Posicao, m.Overall,
                    m.ToTeamId, m.ToTeamId is Guid p2 ? nomes.GetValueOrDefault(p2) : null, m.Amount));
        }

        return porTime
            .Where(t => nomes.ContainsKey(t.Key))
            .Select(t => new TermometroChegadasSaidasDto(
                t.Key, nomes[t.Key],
                t.Value.Chegadas.OrderByDescending(m => m.Data).ToList(),
                t.Value.Saidas.OrderByDescending(m => m.Data).ToList()))
            .OrderByDescending(t => t.Chegadas.Count + t.Saidas.Count)
            .ThenBy(t => t.TimeNome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string RotuloDoTipo(TransferType tipo, bool chegando) => tipo switch
    {
        TransferType.MarketAuction => "Leilão",
        TransferType.TeamSale => chegando ? "Compra" : "Venda",
        TransferType.TeamTrade => "Troca",
        TransferType.QuickSell => "Venda rápida",
        TransferType.ExpansionDraft => "Draft de expansão",
        TransferType.Loan => chegando ? "Empréstimo" : "Emprestado",
        TransferType.LoanReturn => chegando ? "Volta de empréstimo" : "Fim de empréstimo",
        TransferType.LoanPurchase => chegando ? "Compra (opção)" : "Venda (opção)",
        _ => "Movimentação"
    };

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
