using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Fc25Draft.Infra.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DraftDbContext>
{
    public DraftDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? "Development";

        var configuration = BuildConfiguration(environment);
        var connectionString = ConnectionStringResolver.Resolve(
            configuration,
            environment,
            "Connection string 'DefaultConnection' não encontrada (design time).");

        var optionsBuilder = new DbContextOptionsBuilder<DraftDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            options => options.MigrationsAssembly(typeof(DraftDbContext).Assembly.FullName));

        return new DraftDbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration(string environment)
    {
        var basePath = ResolveBasePath();

        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveBasePath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current is not null)
        {
            var directMatch = Path.Combine(current.FullName, "appsettings.json");
            if (File.Exists(directMatch))
            {
                return current.FullName;
            }

            var webProjectPath = Path.Combine(current.FullName, "src", "Fc25Draft.Web");
            if (File.Exists(Path.Combine(webProjectPath, "appsettings.json")))
            {
                return webProjectPath;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
