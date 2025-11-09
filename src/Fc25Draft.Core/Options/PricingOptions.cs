namespace Fc25Draft.Core.Options;

public class PricingOptions
{
    public const string SectionName = "Mercado:Preco";

    public decimal Alpha { get; set; } = 0.8m;
    public decimal Beta { get; set; } = 3.0m;
    public decimal PercentualComprarAgora { get; set; } = 1.5m;
    public decimal PercentualLanceInicial { get; set; } = 0.8m;

    public Dictionary<string, decimal> MultiplicadoresPosicao { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GK"] = 0.95m,
        ["CB"] = 1.05m,
        ["FB"] = 1.05m,
        ["LB"] = 1.1m,
        ["RB"] = 1.05m,
        ["DM"] = 1.05m,
        ["CM"] = 1.10m,
        ["AM"] = 1.15m,
        ["W"] = 1.2m,
        ["ST"] = 1.25m
    };

    public IList<PricingAgeMultiplier> MultiplicadoresIdade { get; set; } = new List<PricingAgeMultiplier>
    {
        new(0, 22, 1.10m),
        new(23, 27, 1.05m),
        new(28, 31, 1.00m),
        new(32, 34, 0.95m),
        new(35, null, 0.90m)
    };
}

public class PricingAgeMultiplier
{
    public PricingAgeMultiplier()
    {
    }

    public PricingAgeMultiplier(int minInclusive, int? maxInclusive, decimal multiplier)
    {
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
        Multiplier = multiplier;
    }

    public int MinInclusive { get; set; }
    public int? MaxInclusive { get; set; }
    public decimal Multiplier { get; set; }

    public bool Matches(int age) => age >= MinInclusive && (MaxInclusive is null || age <= MaxInclusive);
}
