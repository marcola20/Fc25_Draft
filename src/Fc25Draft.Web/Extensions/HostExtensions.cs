using Fc25Draft.Web.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fc25Draft.Web.Extensions
{
    public static class HostExtensions
    {
        public static async Task SeedDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(host);

            using var scope = host.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await SeedData.SeedAsync(context, cancellationToken);
        }
    }
}
