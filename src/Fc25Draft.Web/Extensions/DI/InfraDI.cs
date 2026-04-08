using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fc25Draft.Web.Extensions.DI
{
    public static class InfraDI
    {
        public static IServiceCollection AddInfra(this IServiceCollection services, IConfiguration cfg, IHostEnvironment env)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var cs = ResolveConnectionStringFrom(cfg, env);

            services.AddDbContext<DraftDbContext>(opt =>
                opt.UseNpgsql(cs, npgsql =>
                        npgsql.MigrationsAssembly(typeof(DraftDbContext).Assembly.FullName)
                               .EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null))
                   .EnableSensitiveDataLogging(env.IsDevelopment())
                   .EnableDetailedErrors(env.IsDevelopment()));

            services.AddHealthChecks().AddNpgSql(cs);

            return services;
        }

        private static string ResolveConnectionStringFrom(IConfiguration cfg, IHostEnvironment env)
        {
            var raw = Environment.GetEnvironmentVariable("DATABASE_URL")
                   ?? Environment.GetEnvironmentVariable("POSTGRES_URL")
                   ?? Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL")
                   ?? cfg.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("Não foi possível resolver a connection string do banco.");

            if (!IsPostgresUri(raw) && raw.Contains('='))
            {
                var eqIdx = raw.IndexOf('=');
                var candidate = raw[(eqIdx + 1)..].Trim();
                if (IsPostgresUri(candidate))
                    raw = candidate;
            }

            NpgsqlConnectionStringBuilder builder;

            if (IsPostgresUri(raw))
            {
                var uri = new Uri(raw);
                var userInfo = uri.UserInfo.Split(':', 2);

                builder = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.IsDefaultPort ? 5432 : uri.Port,
                    Database = uri.AbsolutePath.Trim('/'),
                    Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty,
                    Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
                    SslMode = SslMode.Require
                };
            }
            else
            {
                builder = new NpgsqlConnectionStringBuilder(raw);
            }

            if (!env.IsDevelopment())
            {
                if (string.Equals(builder.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(builder.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(builder.Host, "::1", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Localhost não é permitido fora de Development.");
                }

                if (builder.SslMode == SslMode.Disable)
                    builder.SslMode = SslMode.Require;
            }

            return builder.ConnectionString;
        }

        private static bool IsPostgresUri(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                var uri = new Uri(value);
                return uri.Scheme.StartsWith("postgres", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
