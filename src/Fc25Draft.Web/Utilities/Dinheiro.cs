using System.Globalization;

namespace Fc25Draft.Web.Utilities;

public static class Dinheiro
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Valores do mercado são altos: "R$ 51,3 mi" e "R$ 3,2 bi" leem melhor que o número inteiro.</summary>
    public static string Curto(decimal valor)
    {
        var sinal = valor < 0 ? "-" : "";
        var abs = Math.Abs(valor);
        return abs switch
        {
            >= 1_000_000_000 => $"{sinal}R$ {(abs / 1_000_000_000).ToString("0.0", Brasil)} bi",
            >= 1_000_000 => $"{sinal}R$ {(abs / 1_000_000).ToString("0.0", Brasil)} mi",
            _ => sinal + abs.ToString("C0", Brasil)
        };
    }

    /// <summary>Quanto falta até o fim: "termina em 3h", "em 2 dias", "encerrando".</summary>
    public static string Prazo(DateTime fimUtc)
    {
        // Colunas "timestamp with time zone" chegam em hora local (Npgsql em modo legado): normaliza para UTC.
        var fim = fimUtc.Kind == DateTimeKind.Local ? fimUtc.ToUniversalTime() : fimUtc;
        var falta = fim - DateTime.UtcNow;
        return falta.TotalMinutes switch
        {
            <= 0 => "encerrando",
            < 60 => $"termina em {Math.Ceiling(falta.TotalMinutes):0} min",
            < 48 * 60 => $"termina em {Math.Floor(falta.TotalHours):0}h",
            _ => $"termina em {Math.Floor(falta.TotalDays):0} dias"
        };
    }
}
