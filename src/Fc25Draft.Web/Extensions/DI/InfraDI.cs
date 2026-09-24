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

            // Fábrica de contexto (para componentes de vida longa criarem contextos curtos por operação)
            // + shim scoped que mantém as injeções scoped de DraftDbContext existentes funcionando.
            services.AddDbContextFactory<DraftDbContext>(opt =>
                opt.UseNpgsql(cs, npgsql =>
                        npgsql.MigrationsAssembly(typeof(DraftDbContext).Assembly.FullName)
                               .EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null))
                   .EnableSensitiveDataLogging(env.IsDevelopment())
                   .EnableDetailedErrors(env.IsDevelopment()));

            services.AddScoped<DraftDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<DraftDbContext>>().CreateDbContext());

            services.AddHealthChecks().AddNpgSql(cs);

            return services;
        }

        /// <summary>
        /// Registra um serviço que usa o banco com uma conexão (DbContext) só dele, criada pela fábrica.
        /// </summary>
        /// <remarks>
        /// No Blazor Server o <see cref="DraftDbContext"/> scoped é um só para a sessão inteira do navegador:
        /// dois componentes consultando ao mesmo tempo quebravam ("A second operation was started on this
        /// context") e dados rastreados ficavam velhos. Transient: cada componente que injeta o serviço ganha
        /// a sua instância, com a sua conexão. O contexto não é descartado pelo container, mas o EF abre e
        /// fecha a conexão a cada operação, então nada fica preso; ele vai embora junto com o componente.
        /// Só para serviços que não precisam dividir transação com outro serviço.
        /// </remarks>
        public static IServiceCollection AddComConexaoPropria<TService, TImpl>(this IServiceCollection services)
            where TService : class
            where TImpl : class, TService
            => services.AddTransient<TService>(sp => ActivatorUtilities.CreateInstance<TImpl>(
                sp, sp.GetRequiredService<IDbContextFactory<DraftDbContext>>().CreateDbContext()));

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
