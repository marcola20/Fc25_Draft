namespace Fc25Draft.Core.Entities;

/// <summary>
/// Ciclos especiais do mercado. A chave fica em MarketCycle.Theme e a tela do mercado
/// troca o visual (banner, cores, selo) quando o ciclo ativo tem uma delas.
/// </summary>
public static class MarketCycleThemes
{
    public const string TrioDeOuro = "trio-de-ouro";

    public static IReadOnlyList<(string Key, string Label)> All { get; } = new[]
    {
        (TrioDeOuro, "Trio de Ouro"),
    };

    public static bool IsValid(string? key) =>
        string.IsNullOrWhiteSpace(key) || All.Any(t => t.Key == key);

    public static string? LabelOf(string? key) =>
        All.FirstOrDefault(t => t.Key == key).Label;
}
