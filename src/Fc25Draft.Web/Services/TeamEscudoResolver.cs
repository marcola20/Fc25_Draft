using System.Globalization;
using System.Text;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Resolve o caminho do escudo de um time a partir do nome (texto livre).
/// Faz correspondência sem diferenciar maiúsculas/minúsculas nem acentos,
/// de modo que "São Paulo", "Sao Paulo" e "sao paulo" apontem para o mesmo escudo.
/// </summary>
public static class TeamEscudoResolver
{
    public const string DefaultEscudo = "/images/escudos/escudo.png";

    private static readonly Dictionary<string, string> Escudos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Botafogo"] = "/images/escudos/Botafogo.png",
        ["Corinthians"] = "/images/escudos/Corinthians.png",
        ["Coritiba"] = "/images/escudos/Coritiba.png",
        ["Cruzeiro"] = "/images/escudos/Cruzeiro.png",
        ["Flamengo"] = "/images/escudos/Flamengo.png",
        ["Fluminense"] = "/images/escudos/Fluminense.png",
        ["Grêmio"] = "/images/escudos/Gremio.png",
        ["Internacional"] = "/images/escudos/Internacional.png",
        ["Palmeiras"] = "/images/escudos/Palmeiras.png",
        ["Santos"] = "/images/escudos/Santos.png",
        ["São Paulo"] = "/images/escudos/Sao Paulo.png",
        ["Vasco"] = "/images/escudos/Vasco.png",
    };

    // Índice normalizado (sem acentos, minúsculo) para tolerar variações de digitação.
    private static readonly Dictionary<string, string> EscudosNormalizados =
        Escudos.ToDictionary(kv => Normalizar(kv.Key), kv => kv.Value);

    /// <summary>Retorna o caminho do escudo, ou o escudo padrão quando não houver correspondência.</summary>
    public static string GetEscudo(string? nome) => TryGetEscudo(nome, out var path) ? path : DefaultEscudo;

    /// <summary>Indica se existe um escudo cadastrado para o nome informado.</summary>
    public static bool HasEscudo(string? nome) => TryGetEscudo(nome, out _);

    public static bool TryGetEscudo(string? nome, out string path)
    {
        path = DefaultEscudo;
        if (string.IsNullOrWhiteSpace(nome))
            return false;

        var chave = nome.Trim();
        if (Escudos.TryGetValue(chave, out var direto))
        {
            path = direto;
            return true;
        }

        if (EscudosNormalizados.TryGetValue(Normalizar(chave), out var normalizado))
        {
            path = normalizado;
            return true;
        }

        return false;
    }

    private static string Normalizar(string valor)
    {
        var decomposto = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var ch in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
