using Microsoft.EntityFrameworkCore;
using REIGN.Data;

var options = new DbContextOptionsBuilder<ReignDbContext>()
    .UseSqlite("Data Source=REIGN.db")
    .Options;

using var db = new ReignDbContext(options);

var services = await db.Services.ToListAsync();

foreach (var s in services)
{
    Console.WriteLine($"{s.Id} | {s.Name} | Active:{s.Active}");
}
