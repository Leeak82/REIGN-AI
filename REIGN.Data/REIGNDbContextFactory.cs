using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace REIGN.Data;

public class REIGNDbContextFactory : IDesignTimeDbContextFactory<ReignDbContext>
{
    public ReignDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReignDbContext>();
        var connection = Environment.GetEnvironmentVariable("REIGN_CONNECTION")
            ?? $"Data Source={Path.Combine(Directory.GetCurrentDirectory(), "REIGN.db")}";

        optionsBuilder.UseSqlite(connection);

        return new ReignDbContext(optionsBuilder.Options);
    }
}
