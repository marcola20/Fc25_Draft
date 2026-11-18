using System.Globalization;

namespace Fc25Draft.Core.Utilities;

/// <summary>Regras das artes automáticas, sem banco.</summary>
public static class ArtesRegras
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Contratação "hypada": valor a partir do mínimo OU over a partir do mínimo. Troca e empréstimo não têm
    /// valor, então só entram pelo over.
    /// </summary>
    public static bool ContratacaoMereceArte(decimal? valor, int over, decimal valorMinimo, int overMinimo) =>
        (valor is decimal v && v > 0 && v >= valorMinimo) || over >= overMinimo;

    /// <summary>"R$ 45 mi", "R$ 12,5 mi" — curto, para título e arte.</summary>
    public static string Valor(decimal valor) =>
        valor >= 1_000_000
            ? $"R$ {(valor / 1_000_000).ToString("0.#", Br)} mi"
            : valor.ToString("C0", Br);
}
