using System.Globalization;
using System.Text;

namespace Fc25Draft.Web.Utilities;

/// <summary>
/// Normalização usada nas buscas "LIKE" das telas: comparação sem diferenciar
/// maiúsculas/minúsculas nem acentos (digitar "jose" encontra "José").
/// </summary>
public static class TextoBusca
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
