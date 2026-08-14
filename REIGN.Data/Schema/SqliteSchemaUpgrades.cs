using Microsoft.EntityFrameworkCore;

namespace REIGN.Data.Schema;

public static class SqliteSchemaUpgrades
{
    public static async Task ApplyAsync(ReignDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        await EnsureTableAsync(db, """
            CREATE TABLE IF NOT EXISTS "Businesses" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Businesses" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "OwnerName" TEXT NOT NULL DEFAULT '',
                "Phone" TEXT NOT NULL DEFAULT '',
                "Email" TEXT NOT NULL DEFAULT '',
                "Address" TEXT NOT NULL DEFAULT '',
                "Industry" TEXT NOT NULL DEFAULT '',
                "Active" INTEGER NOT NULL DEFAULT 1,
                "Greeting" TEXT NOT NULL DEFAULT '',
                "Tone" TEXT NOT NULL DEFAULT '',
                "Personality" TEXT NOT NULL DEFAULT '',
                "Instructions" TEXT NOT NULL DEFAULT '',
                "Hours" TEXT NOT NULL DEFAULT '',
                "TimeZone" TEXT NOT NULL DEFAULT 'America/New_York',
                "CreatedAt" TEXT NOT NULL
            );
            """, cancellationToken);

        await EnsureTableAsync(db, """
            CREATE TABLE IF NOT EXISTS "BusinessAIProfiles" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_BusinessAIProfiles" PRIMARY KEY,
                "BusinessId" TEXT NOT NULL,
                "AIName" TEXT NOT NULL,
                "Personality" TEXT NOT NULL,
                "Greeting" TEXT NOT NULL,
                "BusinessDescription" TEXT NOT NULL,
                "Active" INTEGER NOT NULL,
                CONSTRAINT "FK_BusinessAIProfiles_Businesses_BusinessId"
                    FOREIGN KEY ("BusinessId") REFERENCES "Businesses" ("Id") ON DELETE CASCADE
            );
            """, cancellationToken);

        await EnsureTableAsync(db, """
            CREATE TABLE IF NOT EXISTS "ConversationStates" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ConversationStates" PRIMARY KEY,
                "CustomerId" TEXT NOT NULL,
                "CurrentStep" TEXT NOT NULL,
                "CurrentIntent" TEXT NULL,
                "LastIntent" TEXT NULL,
                "SelectedService" TEXT NULL,
                "RequestedTime" TEXT NULL,
                "Location" TEXT NULL,
                "Preferences" TEXT NULL,
                "TurnCount" INTEGER NOT NULL DEFAULT 0,
                "LastCustomerMessageAt" TEXT NULL,
                "UpdatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_ConversationStates_Customers_CustomerId"
                    FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
            );
            """, cancellationToken);

        await EnsureTableAsync(db, """
            CREATE TABLE IF NOT EXISTS "CustomerIntentMemories" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CustomerIntentMemories" PRIMARY KEY,
                "CustomerId" TEXT NOT NULL,
                "Intent" TEXT NOT NULL,
                "SelectedService" TEXT NULL,
                "Stage" TEXT NOT NULL,
                "Summary" TEXT NULL,
                "HistoryJson" TEXT NULL,
                "UpdatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_CustomerIntentMemories_Customers_CustomerId"
                    FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
            );
            """, cancellationToken);

        await AddColumnIfMissingAsync(db, "Businesses", "Hours", """ALTER TABLE "Businesses" ADD COLUMN "Hours" TEXT NOT NULL DEFAULT '';""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Businesses", "TimeZone", """ALTER TABLE "Businesses" ADD COLUMN "TimeZone" TEXT NOT NULL DEFAULT 'America/New_York';""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "BusinessId", """ALTER TABLE "Customers" ADD COLUMN "BusinessId" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "HumanOverrideActive", """ALTER TABLE "Customers" ADD COLUMN "HumanOverrideActive" INTEGER NOT NULL DEFAULT 0;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Customers", "HumanOverrideAt", """ALTER TABLE "Customers" ADD COLUMN "HumanOverrideAt" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Services", "BusinessId", """ALTER TABLE "Services" ADD COLUMN "BusinessId" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "Appointments", "ExternalCalendarEventId", """ALTER TABLE "Appointments" ADD COLUMN "ExternalCalendarEventId" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "ConversationMessages", "Source", """ALTER TABLE "ConversationMessages" ADD COLUMN "Source" TEXT NOT NULL DEFAULT '';""", cancellationToken);
        await AddColumnIfMissingAsync(db, "ConversationMessages", "IsOwnerOverride", """ALTER TABLE "ConversationMessages" ADD COLUMN "IsOwnerOverride" INTEGER NOT NULL DEFAULT 0;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "ConversationStates", "CurrentIntent", """ALTER TABLE "ConversationStates" ADD COLUMN "CurrentIntent" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "ConversationStates", "LastIntent", """ALTER TABLE "ConversationStates" ADD COLUMN "LastIntent" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "ConversationStates", "Preferences", """ALTER TABLE "ConversationStates" ADD COLUMN "Preferences" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "ConversationStates", "TurnCount", """ALTER TABLE "ConversationStates" ADD COLUMN "TurnCount" INTEGER NOT NULL DEFAULT 0;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "ConversationStates", "LastCustomerMessageAt", """ALTER TABLE "ConversationStates" ADD COLUMN "LastCustomerMessageAt" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "CustomerIntentMemories", "Summary", """ALTER TABLE "CustomerIntentMemories" ADD COLUMN "Summary" TEXT NULL;""", cancellationToken);
        await AddColumnIfMissingAsync(db, "CustomerIntentMemories", "HistoryJson", """ALTER TABLE "CustomerIntentMemories" ADD COLUMN "HistoryJson" TEXT NULL;""", cancellationToken);

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

    private static async Task EnsureTableAsync(
        ReignDbContext db,
        string createSql,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    private static async Task AddColumnIfMissingAsync(
        ReignDbContext db,
        string table,
        string column,
        string alterSql,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, table, cancellationToken))
        {
            return;
        }

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

    private static async Task<bool> TableExistsAsync(
        ReignDbContext db,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync(cancellationToken);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null && result != DBNull.Value;
    }
}
