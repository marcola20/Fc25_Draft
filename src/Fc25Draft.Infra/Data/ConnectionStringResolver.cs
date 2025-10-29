using System;
using Microsoft.Extensions.Configuration;

namespace Fc25Draft.Infra.Data;

public static class ConnectionStringResolver
{
    private const string DefaultConnectionName = "DefaultConnection";

    public static string Resolve(IConfiguration configuration, string environmentName, string? missingMessage = null)
    {
        var fromConfig = configuration.GetConnectionString(DefaultConnectionName);
        var fromEnvLegacy = Environment.GetEnvironmentVariable($"SQLCONNSTR_{DefaultConnectionName}");
        var fromEnvModern = Environment.GetEnvironmentVariable($"ConnectionStrings__{DefaultConnectionName}");

        var conn = fromEnvModern ?? fromEnvLegacy ?? fromConfig;

        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException(missingMessage ?? $"Connection string '{DefaultConnectionName}' não encontrada.");
        }

        var isDevelopment = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);

        if (!isDevelopment && ContainsLocalhost(conn))
        {
            throw new InvalidOperationException("Em Production a connection não pode apontar para localhost.");
        }

        return conn;
    }

    private static bool ContainsLocalhost(string connectionString)
    {
        return connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase)
               || connectionString.Contains("127.", StringComparison.OrdinalIgnoreCase);
    }
}
