namespace Fc25Draft.Core.Options;

public class EconomiaOptions
{
    public const string SectionName = "Mercado:Economia";

    public decimal PremioVitoria { get; set; } = 3_000_000.00m;

    public decimal PremioEmpate { get; set; } = 1_000_000.00m;

    public decimal PremioGolMarcado { get; set; } = 200_000.00m;

    public decimal PremioCleanSheet { get; set; } = 500_000.00m;

    public decimal PenalidadeGolSofrido { get; set; } = 100_000.00m;
}
