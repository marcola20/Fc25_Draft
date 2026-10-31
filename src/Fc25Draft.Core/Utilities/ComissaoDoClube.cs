namespace Fc25Draft.Core.Utilities;

/// <summary>Profissional da comissão técnica que o clube contrata para a temporada.</summary>
public enum CargoDaComissao : short
{
    TreinadorDaBase = 1,
    PreparadorFisico = 2,
    TreinadorDeGoleiros = 3
}

/// <summary>
/// Comissão técnica: o clube paga com o caixa, no começo da temporada, um profissional que dá vantagem a temporada
/// inteira (no treino da semana ou na evolução da virada).
/// </summary>
public static class ComissaoCriterios
{
    /// <summary>A comissão começa nesta temporada.</summary>
    public const int ValeAPartirDaTemporada = 2011;

    /// <summary>Dá para contratar até tantos dias depois do início da temporada (a pré-temporada).</summary>
    public const int DiasParaContratar = 21;

    /// <summary>Treinador da base: vagas no treino da semana e idade máxima de quem treina.</summary>
    public const int VagasComTreinadorDaBase = 4;
    public const int IdadeComTreinadorDaBase = 26;

    /// <summary>Preparador físico: quantos jogadores (os mais velhos do elenco) caem menos na virada, e quanto.</summary>
    public const int VeteranosDoPreparador = 3;
    public const int BonusDoPreparador = 1;

    /// <summary>Treinador de goleiros: o goleiro ganha isso a mais em cada semana de treino (não soma com o padrinho).</summary>
    public const int ExtraDoTreinadorDeGoleiros = 1;

    public static decimal Preco(CargoDaComissao cargo) => cargo switch
    {
        CargoDaComissao.TreinadorDaBase => 8_000_000m,
        CargoDaComissao.PreparadorFisico => 6_000_000m,
        _ => 5_000_000m
    };
}

public static class ComissaoDoClube
{
    public static IReadOnlyList<CargoDaComissao> Cargos { get; } =
        [CargoDaComissao.TreinadorDaBase, CargoDaComissao.PreparadorFisico, CargoDaComissao.TreinadorDeGoleiros];

    public static string Emoji(CargoDaComissao cargo) => cargo switch
    {
        CargoDaComissao.TreinadorDaBase => "🌱",
        CargoDaComissao.PreparadorFisico => "💪",
        _ => "🧤"
    };

    public static string Nome(CargoDaComissao cargo) => cargo switch
    {
        CargoDaComissao.TreinadorDaBase => "Treinador da base",
        CargoDaComissao.PreparadorFisico => "Preparador físico",
        _ => "Treinador de goleiros"
    };

    public static string OQueFaz(CargoDaComissao cargo) => cargo switch
    {
        CargoDaComissao.TreinadorDaBase =>
            $"o treino da semana passa a ter {ComissaoCriterios.VagasComTreinadorDaBase} vagas e aceita jogadores de até {ComissaoCriterios.IdadeComTreinadorDaBase} anos",
        CargoDaComissao.PreparadorFisico =>
            $"os {ComissaoCriterios.VeteranosDoPreparador} jogadores mais velhos do elenco caem {ComissaoCriterios.BonusDoPreparador} a menos na evolução da virada",
        _ => $"o goleiro ganha +{ComissaoCriterios.ExtraDoTreinadorDeGoleiros} a mais em cada semana de treino (não soma com o padrinho)"
    };
}
