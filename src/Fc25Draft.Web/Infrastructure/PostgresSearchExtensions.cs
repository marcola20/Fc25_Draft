using System;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Infrastructure;

public static class PostgresSearchExtensions
{
    public const string AccentSource = "ÁÀÃÄÂáàãäâÉÈÊËéèêëÍÌÎÏíìîïÓÒÕÖÔóòõöôÚÙÛÜúùûüÇçÑñ";
    public const string AccentReplacement = "AAAAAaaaaaEEEEeeeeIIIIiiiiOOOOOoooooUUUUuuuuCcNn";

    [DbFunction("translate", IsBuiltIn = true)]
    public static string? Translate(this DbFunctions _, string? value, string? from, string? to)
        => throw new NotSupportedException("This method is intended for use with Entity Framework Core LINQ queries.");

    public static string NormalizeForSearch(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            var index = AccentSource.IndexOf(ch);
            builder.Append(index >= 0 ? AccentReplacement[index] : ch);
        }

        return builder.ToString();
    }
}
