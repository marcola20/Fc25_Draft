namespace Fc25Draft.Core.Options;

public class MarketGenerationOptions
{
    public const string SectionName = "Mercado:Geracao";

    public int QuantidadePorRodada { get; set; } = 8;

    public int ComunsFaixaMin { get; set; } = 77;

    public int ComunsFaixaMax { get; set; } = 79;

    public int ComunsQuantidade { get; set; } = 6;

    public int IntermediarioFaixaMin { get; set; } = 80;

    public int IntermediarioFaixaMax { get; set; } = 81;

    public int IntermediarioQuantidade { get; set; } = 1;

    public int RaroFaixaMin { get; set; } = 77;

    public int RaroFaixaMax { get; set; } = 99;

    public int RaroQuantidade { get; set; } = 1;

    public int JanelaProtecaoMinutos { get; set; } = 5;
}
