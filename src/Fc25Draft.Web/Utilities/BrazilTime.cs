using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Fc25Draft.Web.Utilities;

public static class BrazilTime
{
    public const string TimeZoneDisplayName = "UTC-3";

    public static CultureInfo Culture { get; } = new("pt-BR");

    public static TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "E. South America Standard Time"
            : "America/Sao_Paulo");

    public static DateTime ConvertFromUtc(DateTime utc)
    {
        var normalized = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(normalized, TimeZone);
    }

    public static string FormatDateTime(DateTime utc, string format = "dd/MM/yyyy HH:mm")
    {
        var local = ConvertFromUtc(utc);
        return local.ToString(format, Culture);
    }

    public static string FormatDateTimeWithZone(DateTime utc)
    {
        var local = ConvertFromUtc(utc);
        return $"{local:dd/MM/yyyy HH:mm} ({TimeZoneDisplayName})";
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
