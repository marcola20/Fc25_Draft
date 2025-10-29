using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Fc25Draft.Infra.Data;

public static class ConnectionStringResolver
{
    private const string DefaultConnectionName = "DefaultConnection";

    public static string Resolve(
        IConfiguration configuration,
        string? environmentName,
        string? missingMessage = null)
    {
        var effectiveEnvironmentName = ResolveEnvironmentName(environmentName);

        var fromConfig = configuration.GetConnectionString(DefaultConnectionName);
        var fromEnvLegacy = Environment.GetEnvironmentVariable($"SQLCONNSTR_{DefaultConnectionName}");
        var fromEnvModern = Environment.GetEnvironmentVariable($"ConnectionStrings__{DefaultConnectionName}");

        var conn = fromEnvModern ?? fromEnvLegacy ?? fromConfig;

        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException(missingMessage ?? $"Connection string '{DefaultConnectionName}' não encontrada.");
        }

        var isDevelopment = string.Equals(
            effectiveEnvironmentName,
            Environments.Development,
            StringComparison.OrdinalIgnoreCase);

        if (!isDevelopment && ContainsLocalhost(conn))
        {
            throw new InvalidOperationException("Em Production a connection não pode apontar para localhost.");
        }

        return conn;
    }

    private static string ResolveEnvironmentName(string? environmentName)
    {
        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            return environmentName.Trim();
        }

        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
               ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
               ?? Environments.Production;
    }

    private static bool ContainsLocalhost(string connectionString)
    {
        return connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase)
               || connectionString.Contains("127.", StringComparison.OrdinalIgnoreCase);
    }
}
