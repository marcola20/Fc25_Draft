using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<DraftDbContext>
{
    public DraftDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<DraftDbContext>();
        var cs = "Server=(localdb)\\MSSQLLocalDB;Database=Fc25DraftLocal;Trusted_Connection=True;MultipleActiveResultSets=true";
        builder.UseSqlServer(cs, sql => sql.MigrationsAssembly(typeof(SqlServerDesignTimeFactory).Assembly.FullName));
        return new DraftDbContext(builder.Options);
    }
}
