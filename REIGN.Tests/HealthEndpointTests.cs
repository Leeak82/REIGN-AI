using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using REIGN.API.AI;
using REIGN.API.Controllers;
using REIGN.API.Options;
using REIGN.Core.AI;
using REIGN.Core.Services;
using REIGN.Data;
using REIGN.Data.Schema;
using Xunit;

namespace REIGN.Tests;

public class HealthEndpointTests
{
    [Fact]
    public async Task Production_health_includes_calendar_provider_and_hides_secrets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new ReignDbContext(new DbContextOptionsBuilder<ReignDbContext>().UseSqlite(connection).Options);
        await SqliteSchemaUpgrades.ApplyAsync(db);

        var controller = new HealthController(
            new FallbackAiProvider(new ConversationAIService(), new ReignAssistant()),
            new ConfigurationBuilder().Build(),
            db,
            Options.Create(new AiOptions()),
            Options.Create(new SmsOptions { Provider = "Simulated" }),
            Options.Create(new GoogleCalendarOptions
            {
                Provider = "Google",
                ClientId = "health-client-id",
                ClientSecret = "super-secret-calendar"
            }));

        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await controller.Production());
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var root = doc.RootElement;

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal("connected", root.GetProperty("database").GetString());
        Assert.True(root.GetProperty("calendarConfigured").GetBoolean());
        Assert.Equal("Google", root.GetProperty("calendarProvider").GetString());
        Assert.False(root.TryGetProperty("clientId", out _));
        Assert.False(root.TryGetProperty("clientSecret", out _));
        Assert.DoesNotContain("super-secret-calendar", JsonSerializer.Serialize(result.Value), StringComparison.Ordinal);
        Assert.DoesNotContain("health-client-id", JsonSerializer.Serialize(result.Value), StringComparison.Ordinal);
    }
}
