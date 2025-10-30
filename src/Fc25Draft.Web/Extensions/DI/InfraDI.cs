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

        /// <summary>
        /// Resolve a connection string compatível com ambientes locais e com DATABASE_URL (Render/Azure/etc).
        /// </summary>
        private static string ResolveConnectionStringFrom(IConfiguration cfg, IHostEnvironment env)
        {
            var raw = Environment.GetEnvironmentVariable("DATABASE_URL");

            raw ??= cfg.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("Não foi possível resolver a connection string do banco.");

            NpgsqlConnectionStringBuilder builder;

            if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(raw);
                var userInfo = uri.UserInfo.Split(':', 2);

                builder = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.IsDefaultPort ? 5432 : uri.Port,
                    Database = uri.AbsolutePath.Trim('/'),
                    Username = userInfo[0],
                    Password = userInfo.Length > 1 ? userInfo[1] : string.Empty,
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
    }
}
