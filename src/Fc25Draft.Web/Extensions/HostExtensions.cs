using Fc25Draft.Infra.Data;             
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Extensions
{
    public static class HostExtensions
    {
        public static async Task SeedDatabaseAsync(this WebApplication app, CancellationToken ct = default)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();

            await db.Database.MigrateAsync(ct);

            await SeedData.SeedAsync(db, ct);

            if (app.Environment.IsDevelopment())
            {
                await SeedData.SeedTeamBudgetsAsync(db, ct);
            }
        }
    }
}
