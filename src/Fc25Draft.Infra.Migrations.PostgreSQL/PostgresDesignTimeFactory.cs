using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<DraftDbContext>
{
    public DraftDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<DraftDbContext>();
        var cs = "Host=localhost;Port=5432;Database=fc25draft_dev_pg;Username=postgres;Password=postgres";
        builder.UseNpgsql(cs, npg => npg.MigrationsAssembly(typeof(PostgresDesignTimeFactory).Assembly.FullName));
        return new DraftDbContext(builder.Options);
    }
}
