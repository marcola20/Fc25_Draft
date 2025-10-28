using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;

namespace Fc25Draft.Web.Shared;

public class InputDecimalPtBr : InputBase<decimal?>
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    protected override bool TryParseValueFromString(string? value, out decimal? result, out string? validationErrorMessage)
    {
        var input = value?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            result = null;
            validationErrorMessage = null;
            return true;
        }

        if (decimal.TryParse(input, NumberStyles.Number, PtBr, out var parsed) ||
            decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            result = parsed;
            validationErrorMessage = null;
            return true;
        }

        result = null;
        validationErrorMessage = $"Valor inválido.";
        return false;
    }

    protected override string? FormatValueAsString(decimal? value)
    {
        return value?.ToString("N2", PtBr) ?? string.Empty;
    }
}
