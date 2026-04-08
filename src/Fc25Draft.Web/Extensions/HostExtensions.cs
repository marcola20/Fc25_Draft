using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Web.Extensions
{
    public static class HostExtensions
    {
        public static async Task SeedDatabaseAsync(this WebApplication app, CancellationToken ct = default)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
            var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>();

            await db.Database.MigrateAsync(ct);

            if (appOptions.Value.EnableDevSeed)
            {
                await SeedData.SeedAsync(db, ct);

                if (app.Environment.IsDevelopment())
                {
                    await SeedData.SeedTeamBudgetsAsync(db, ct);
                }
            }
        }
    }
}
