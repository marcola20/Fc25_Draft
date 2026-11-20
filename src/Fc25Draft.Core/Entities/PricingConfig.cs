namespace Fc25Draft.Core.Entities;

/// <summary>
/// Parâmetros de precificação do mercado, editáveis pela página de Configurações (linha única).
/// Os pesos por posição continuam em <c>MarketWeightResolver</c>.
/// </summary>
public class PricingConfig
{
    public int Id { get; set; } // singleton, sempre 1

    public decimal BaseScale { get; set; }
    public decimal OverallBase { get; set; }
    public int OverallPivot { get; set; }
    public decimal BuyNowFactor { get; set; }
    public decimal MinIncrementRate { get; set; }
    public decimal MinIncrementStep { get; set; }

    // Fatores de idade (faixas fixas)
    public decimal AgeFactorUpTo22 { get; set; }
    public decimal AgeFactor23To24 { get; set; }
    public decimal AgeFactor25To26 { get; set; }
    public decimal AgeFactor27To28 { get; set; }
    public decimal AgeFactor29To30 { get; set; }
    public decimal AgeFactor31To32 { get; set; }
    public decimal AgeFactor33To34 { get; set; }
    public decimal AgeFactor35Plus { get; set; }

    public DateTime AtualizadoEm { get; set; }

    /// <summary>Valores padrão (equivalentes ao comportamento anterior, hardcoded).</summary>
    public static PricingConfig Default() => new()
    {
        Id = 1,
        BaseScale = 10_000_000m,
        OverallBase = 1.08m,
        OverallPivot = 75,
        BuyNowFactor = 1.80m,
        MinIncrementRate = 0.03m,
        MinIncrementStep = 500m,
        AgeFactorUpTo22 = 1.18m,
        AgeFactor23To24 = 1.15m,
        AgeFactor25To26 = 1.10m,
        AgeFactor27To28 = 1.00m,
        AgeFactor29To30 = 0.98m,
        AgeFactor31To32 = 0.95m,
        AgeFactor33To34 = 0.90m,
        AgeFactor35Plus = 0.85m
    };

    /// <summary>Fator de idade conforme as faixas fixas.</summary>
    public decimal AgeFactor(int age) => age switch
    {
        <= 22 => AgeFactorUpTo22,
        <= 24 => AgeFactor23To24,
        <= 26 => AgeFactor25To26,
        <= 28 => AgeFactor27To28,
        <= 30 => AgeFactor29To30,
        <= 32 => AgeFactor31To32,
        <= 34 => AgeFactor33To34,
        _ => AgeFactor35Plus
    };
}
