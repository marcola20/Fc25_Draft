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

        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var fromConfig = cfg.GetConnectionString("DefaultConnection");
        var fromEnvLegacy = Environment.GetEnvironmentVariable("SQLCONNSTR_DefaultConnection");
        var fromEnvModern = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var conn = fromEnvModern ?? fromEnvLegacy ?? fromConfig;

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("Connection string not found for design-time context.");

        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new DraftDbContext(options);
    }
}
