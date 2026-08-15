using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace REIGN.Data;

public class REIGNDbContextFactory : IDesignTimeDbContextFactory<ReignDbContext>
{
    public ReignDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReignDbContext>();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Reign");
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = $"Data Source={Path.Combine(Directory.GetCurrentDirectory(), "REIGN.db")}";
        }

        if (DatabaseConnection.IsPostgreSql(connection))
        {
            optionsBuilder.UseNpgsql(DatabaseConnection.Normalize(connection));
        }
        else
        {
            optionsBuilder.UseSqlite(connection);
        }

        return new ReignDbContext(optionsBuilder.Options);
    }
}
