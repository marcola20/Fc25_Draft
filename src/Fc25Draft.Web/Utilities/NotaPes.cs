using System.Globalization;

namespace Fc25Draft.Web.Utilities;

/// <summary>Como a nota do PES aparece: sempre com uma casa ("6,5") e a cor pela faixa.</summary>
public static class NotaPes
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static string Texto(decimal nota) => nota.ToString("0.0", Br);

    public static string Media(decimal media) => media.ToString("0.00", Br);

    /// <summary>Classe do badge: 7 ou mais é bom jogo, abaixo de 6 é jogo ruim.</summary>
    public static string Cor(decimal nota) => nota switch
    {
        >= 7m => "bg-success",
        >= 6m => "bg-secondary",
        _ => "bg-danger"
    };
}
