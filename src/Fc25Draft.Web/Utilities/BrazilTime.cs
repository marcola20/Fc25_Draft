using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Fc25Draft.Web.Utilities;

public static class BrazilTime
{

    public static CultureInfo Culture { get; } = new("pt-BR");

    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "E. South America Standard Time"
            : "America/Sao_Paulo");

    public static DateTime ConvertFromUtc(DateTime utc)
    {
        var normalized = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(normalized, Zone);
    }

    public static string FormatDateTime(DateTime utc, string format = "dd/MM/yyyy HH:mm")
    {
        var local = ConvertFromUtc(utc);
        return local.ToString(format, Culture);
    }

    public static string FormatDateTimeLocal(DateTime utc, string format = "dd/MM/yyyy HH:mm")
        => FormatDateTime(utc, format);

    public static string FormatDateTimeWithZone(DateTime utc)
        => FormatDateTime(utc);

    public static DateTime ConvertToUtc(DateTime local)
    {
        return local.Kind switch
        {
            DateTimeKind.Utc => local,
            DateTimeKind.Local => TimeZoneInfo.ConvertTimeToUtc(local, Zone),
            _ => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone)
        };
    }

    public static bool TryParseDateTime(string? input, out DateTime resultUtc)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            resultUtc = default;
            return false;
        }

        var formats = new[]
        {
            "yyyy-MM-dd'T'HH:mm",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd'T'HH:mmK",
            "yyyy-MM-dd'T'HH:mm:ssK"
        };

        if (DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedUtc))
        {
            resultUtc = DateTime.SpecifyKind(parsedUtc, DateTimeKind.Utc);
            return true;
        }

        if (DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedLocal))
        {
            var unspecified = DateTime.SpecifyKind(parsedLocal, DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZone);
            resultUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return true;
        }

        resultUtc = default;
        return false;
    }
}
