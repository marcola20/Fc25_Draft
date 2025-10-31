using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fc25Draft.Web.Utilities;

public sealed class QueryStringBuilder
{
    private readonly List<KeyValuePair<string, string>> _parameters = new();

    public QueryStringBuilder Add(string name, Guid? value)
    {
        if (value.HasValue && value.Value != Guid.Empty)
        {
            _parameters.Add(new KeyValuePair<string, string>(name, value.Value.ToString()));
        }

        return this;
    }

    public QueryStringBuilder Add(string name, int? value)
    {
        if (value.HasValue)
        {
            _parameters.Add(new KeyValuePair<string, string>(name, value.Value.ToString(CultureInfo.InvariantCulture)));
        }

        return this;
    }

    public QueryStringBuilder Add(string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
        {
            _parameters.Add(new KeyValuePair<string, string>(name, value.Trim()));
        }

        return this;
    }

    public QueryStringBuilder AddInvariant(string name, int value)
    {
        _parameters.Add(new KeyValuePair<string, string>(name, value.ToString(CultureInfo.InvariantCulture)));
        return this;
    }

    public QueryStringBuilder AddInvariant(string name, long value)
    {
        _parameters.Add(new KeyValuePair<string, string>(name, value.ToString(CultureInfo.InvariantCulture)));
        return this;
    }

    public QueryStringBuilder AddInvariant(string name, decimal value)
    {
        _parameters.Add(new KeyValuePair<string, string>(name, value.ToString(CultureInfo.InvariantCulture)));
        return this;
    }

    public QueryStringBuilder AddRange(string name, IEnumerable<string?>? values)
    {
        if (values is null)
        {
            return this;
        }

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _parameters.Add(new KeyValuePair<string, string>(name, value.Trim()));
            }
        }

        return this;
    }

    public QueryStringBuilder Add(string name, DateTime? value)
    {
        if (value.HasValue)
        {
            var utc = value.Value;
            if (utc.Kind == DateTimeKind.Unspecified)
            {
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            }

            if (utc.Kind != DateTimeKind.Utc)
            {
                utc = utc.ToUniversalTime();
            }

            _parameters.Add(new KeyValuePair<string, string>(name, utc.ToString("O", CultureInfo.InvariantCulture)));
        }

        return this;
    }

    public string Build()
    {
        if (_parameters.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var first = true;
        foreach (var (key, value) in _parameters)
        {
            if (!first)
            {
                sb.Append('&');
            }

            first = false;
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value));
        }

        return sb.ToString();
    }

    public override string ToString() => Build();
}
