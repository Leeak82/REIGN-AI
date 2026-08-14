using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace REIGN.Data.Schema;

public static class SqliteSchemaUpgrades
{
    public static async Task ApplyAsync(ReignDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        await AddColumnIfMissingAsync(db, "Customers", "HumanOverrideActive", """ALTER TABLE "Customers" ADD COLUMN "HumanOverrideActive" INTEGER NOT NULL DEFAULT 0;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "HumanOverrideAt", """ALTER TABLE "Customers" ADD COLUMN "HumanOverrideAt" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Appointments", "ExternalCalendarEventId", """ALTER TABLE "Appointments" ADD COLUMN "ExternalCalendarEventId" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "ConversationMessages", "Source", """ALTER TABLE "ConversationMessages" ADD COLUMN "Source" TEXT NOT NULL DEFAULT '';""", cancellationToken);
        await AddColumnIfMissingAsync(db, "ConversationMessages", "IsOwnerOverride", """ALTER TABLE "ConversationMessages" ADD COLUMN "IsOwnerOverride" INTEGER NOT NULL DEFAULT 0;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "CurrentIntent", """ALTER TABLE "Customers" ADD COLUMN "CurrentIntent" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "LastIntent", """ALTER TABLE "Customers" ADD COLUMN "LastIntent" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "PendingServiceName", """ALTER TABLE "Customers" ADD COLUMN "PendingServiceName" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "ConversationStatus", """ALTER TABLE "Customers" ADD COLUMN "ConversationStatus" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "TurnCount", """ALTER TABLE "Customers" ADD COLUMN "TurnCount" INTEGER NOT NULL DEFAULT 0;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "LastCustomerMessageAt", """ALTER TABLE "Customers" ADD COLUMN "LastCustomerMessageAt" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "IntentHistory", """ALTER TABLE "Customers" ADD COLUMN "IntentHistory" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "MemorySummary", """ALTER TABLE "Customers" ADD COLUMN "MemorySummary" TEXT NULL;""", cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS IntegrationTokens (
                Id TEXT NOT NULL PRIMARY KEY,
                Provider TEXT NOT NULL,
                AccessToken TEXT NOT NULL,
                RefreshToken TEXT NOT NULL,
                AccessTokenExpiresAt TEXT NULL,
                TokenType TEXT NULL,
                Scope TEXT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """, cancellationToken);
    }

    private static async Task AddColumnIfMissingAsync(
        ReignDbContext db,
        string table,
        string column,
        string alterSql,
        CancellationToken cancellationToken)
    {
        var exists = false;
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";

        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync(cancellationToken);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            await db.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
        }
    }
}
