using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace REIGN.Data;

public class REIGNDbContextFactory : IDesignTimeDbContextFactory<ReignDbContext>
{
    public ReignDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReignDbContext>();

        optionsBuilder.UseSqlite("Data Source=REIGN.db");

        return new ReignDbContext(optionsBuilder.Options);
    }
}