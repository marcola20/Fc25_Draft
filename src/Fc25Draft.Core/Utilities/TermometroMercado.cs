using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Utilities;

/// <summary>Item leiloado no período, com o resultado (vencedor e preço final) quando vendido.</summary>
public record TermometroItemInput(
    Guid ItemId, int PlayerId, string JogadorNome, string Posicao, int Overall,
    decimal PrecoBase, decimal? PrecoCompraImediata, bool Vendido, Guid? VencedorId, decimal? PrecoFinal);

public record TermometroLanceInput(Guid ItemId, Guid TeamId, decimal Valor);

/// <summary>Transferência com dinheiro envolvido (leilão, venda entre times, venda rápida).</summary>
public record TermometroTransferenciaInput(
    DateTime Data, TransferType Tipo, int PlayerId, string JogadorNome, string Posicao,
    Guid? DeTimeId, Guid? ParaTimeId, decimal Valor);

public record TermometroDisputaDto(
    int PlayerId, string JogadorNome, string Posicao, int Overall,
    int Lances, int Times, decimal PrecoBase, decimal? PrecoFinal, string? VencedorNome, bool CompraImediata)
{
    // Quanto acima do preço base saiu (1,5 = 50% acima).
    public double? Agio => PrecoFinal is decimal final && PrecoBase > 0 ? (double)(final / PrecoBase) : null;
}

public record TermometroContratacaoDto(
    DateTime Data, TransferType Tipo, int PlayerId, string JogadorNome, string Posicao,
    string? DeTime, string? ParaTime, decimal Valor);

public record TermometroTimeDto(
    Guid TimeId, string TimeNome,
    int Compras, decimal Gasto, int Vendas, decimal Recebido,
    int Lances, int LeiloesVencidos)
{
    public decimal Saldo => Recebido - Gasto;
}

public record TermometroPosicaoDto(string Posicao, int Contratacoes, decimal Gasto);

public record TermometroMercadoDto(
    decimal Movimentado,
    int Transferencias,
    int LeiloesVendidos,
    int ItensLeiloados,
    int Lances,
    int ComprasImediatas,
    // Média de preço final / preço base nos leilões vendidos (1,3 = 30% acima).
    double? AgioMedio,
    IReadOnlyList<TermometroDisputaDto> MaisDisputados,
    IReadOnlyList<TermometroContratacaoDto> MaioresContratacoes,
    IReadOnlyList<TermometroDisputaDto> Pechinchas,
    IReadOnlyList<TermometroTimeDto> Times,
    IReadOnlyList<TermometroPosicaoDto> Posicoes);

/// <summary>Números do mercado num período: disputa nos leilões, maiores compras, gasto por time e por posição.</summary>
public static class TermometroMercado
{
    public const int Top = 8;
    public const int TopPechinchas = 5;

    public static TermometroMercadoDto Calcular(
        IReadOnlyList<TermometroItemInput> itens,
        IReadOnlyList<TermometroLanceInput> lances,
        IReadOnlyList<TermometroTransferenciaInput> transferencias,
        IReadOnlyDictionary<Guid, string> nomes)
    {
        string? Nome(Guid? id) => id is Guid g && nomes.TryGetValue(g, out var n) ? n : null;

        var lancesPorItem = lances.GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.ToList());

        var disputas = itens
            .Select(i =>
            {
                var dele = lancesPorItem.GetValueOrDefault(i.ItemId) ?? new List<TermometroLanceInput>();
                var imediata = i.Vendido && i.PrecoCompraImediata is decimal bn && i.PrecoFinal == bn;
                return new TermometroDisputaDto(i.PlayerId, i.JogadorNome, i.Posicao, i.Overall,
                    dele.Count, dele.Select(l => l.TeamId).Distinct().Count(),
                    i.PrecoBase, i.Vendido ? i.PrecoFinal : null, i.Vendido ? Nome(i.VencedorId) : null, imediata);
            })
            .ToList();

        var vendidas = disputas.Where(d => d.PrecoFinal is not null && d.Agio is not null).ToList();

        // "Compra" = pagou para trazer o jogador; "venda" = recebeu para deixá-lo sair.
        var compras = transferencias.Where(t => t.Tipo is TransferType.MarketAuction or TransferType.TeamSale).ToList();
        var vendas = transferencias.Where(t => t.Tipo is TransferType.TeamSale or TransferType.QuickSell).ToList();

        var timeIds = compras.Where(c => c.ParaTimeId is not null).Select(c => c.ParaTimeId!.Value)
            .Concat(vendas.Where(v => v.DeTimeId is not null).Select(v => v.DeTimeId!.Value))
            .Concat(lances.Select(l => l.TeamId))
            .Distinct();

        var times = timeIds
            .Select(id => new TermometroTimeDto(
                id,
                Nome(id) ?? "?",
                compras.Count(c => c.ParaTimeId == id),
                compras.Where(c => c.ParaTimeId == id).Sum(c => c.Valor),
                vendas.Count(v => v.DeTimeId == id),
                vendas.Where(v => v.DeTimeId == id).Sum(v => v.Valor),
                lances.Count(l => l.TeamId == id),
                itens.Count(i => i.Vendido && i.VencedorId == id)))
            .OrderByDescending(t => t.Gasto)
            .ThenByDescending(t => t.Lances)
            .ThenBy(t => t.TimeNome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TermometroMercadoDto(
            compras.Sum(c => c.Valor),
            compras.Count,
            vendidas.Count,
            itens.Count,
            lances.Count,
            disputas.Count(d => d.CompraImediata),
            vendidas.Count == 0 ? null : vendidas.Average(d => d.Agio!.Value),
            disputas
                .Where(d => d.Lances > 0)
                .OrderByDescending(d => d.Lances)
                .ThenByDescending(d => d.Times)
                .ThenByDescending(d => d.PrecoFinal ?? 0)
                .Take(Top)
                .ToList(),
            compras
                .OrderByDescending(c => c.Valor)
                .ThenByDescending(c => c.Data)
                .Take(Top)
                .Select(c => new TermometroContratacaoDto(c.Data, c.Tipo, c.PlayerId, c.JogadorNome, c.Posicao,
                    Nome(c.DeTimeId), Nome(c.ParaTimeId), c.Valor))
                .ToList(),
            // Pechincha: saiu mais perto do preço base, com mais overall desempatando.
            vendidas
                .OrderBy(d => d.Agio)
                .ThenByDescending(d => d.Overall)
                .Take(TopPechinchas)
                .ToList(),
            times,
            compras
                .GroupBy(c => c.Posicao)
                .Select(g => new TermometroPosicaoDto(g.Key, g.Count(), g.Sum(c => c.Valor)))
                .OrderByDescending(p => p.Gasto)
                .ToList());
    }
}
