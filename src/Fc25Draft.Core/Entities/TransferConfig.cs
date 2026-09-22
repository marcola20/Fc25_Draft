namespace Fc25Draft.Core.Entities;

/// <summary>
/// Limites de transferências/mercado, editáveis em /admin/configuracoes (linha única).
/// </summary>
public class TransferConfig
{
    public int Id { get; set; } // singleton, sempre 1

    /// <summary>Máximo de vendas rápidas (quick sell) por janela.</summary>
    public int MaxQuickSellPerWindow { get; set; }

    /// <summary>Máximo de transferências por janela (por time).</summary>
    public int MaxTransfers { get; set; }

    /// <summary>Quantidade mínima de jogadores que um elenco deve manter.</summary>
    public int MinRosterSize { get; set; }

    public DateTime AtualizadoEm { get; set; }

    /// <summary>Mínimo de elenco que vale para o time: o temporário dele, se houver, senão o geral.</summary>
    public int MinRosterSizeFor(Team team) => team.MinRosterSizeOverride ?? MinRosterSize;

    /// <summary>Limite de vendas rápidas que vale para o time: o individual dele, se houver, senão o geral.</summary>
    public int MaxQuickSellFor(Team team) => team.QuickSellLimitOverride ?? MaxQuickSellPerWindow;

    /// <summary>Valores padrão (equivalentes ao comportamento anterior, hardcoded).</summary>
    public static TransferConfig Default() => new()
    {
        Id = 1,
        MaxQuickSellPerWindow = 5,
        MaxTransfers = 5,
        MinRosterSize = 14
    };
}
