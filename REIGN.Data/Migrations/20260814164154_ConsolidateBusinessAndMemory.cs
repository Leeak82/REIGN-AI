using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REIGN.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateBusinessAndMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hours",
                table: "Businesses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Businesses",
                type: "TEXT",
                nullable: false,
                defaultValue: "America/New_York");

            migrationBuilder.AddColumn<string>(
                name: "CurrentIntent",
                table: "ConversationStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastIntent",
                table: "ConversationStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Preferences",
                table: "ConversationStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TurnCount",
                table: "ConversationStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCustomerMessageAt",
                table: "ConversationStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "CustomerIntentMemories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoryJson",
                table: "CustomerIntentMemories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO "ConversationStates"
                    ("Id", "CustomerId", "CurrentStep", "CurrentIntent", "LastIntent", "SelectedService", "RequestedTime", "Location", "Preferences", "TurnCount", "LastCustomerMessageAt", "UpdatedAt")
                SELECT
                    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6))),
                    c."Id",
                    COALESCE(c."ConversationStatus", 'Active'),
                    c."CurrentIntent",
                    c."LastIntent",
                    c."PendingServiceName",
                    NULL,
                    NULL,
                    c."Notes",
                    COALESCE(c."TurnCount", 0),
                    c."LastCustomerMessageAt",
                    datetime('now')
                FROM "Customers" c
                WHERE NOT EXISTS (
                    SELECT 1 FROM "ConversationStates" s WHERE s."CustomerId" = c."Id"
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "CustomerIntentMemories"
                    ("Id", "CustomerId", "Intent", "SelectedService", "Stage", "Summary", "HistoryJson", "UpdatedAt")
                SELECT
                    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6))),
                    c."Id",
                    COALESCE(c."LastIntent", c."CurrentIntent", 'Unknown'),
                    c."PendingServiceName",
                    COALESCE(c."ConversationStatus", 'Active'),
                    c."MemorySummary",
                    c."IntentHistory",
                    datetime('now')
                FROM "Customers" c
                WHERE NOT EXISTS (
                    SELECT 1 FROM "CustomerIntentMemories" m WHERE m."CustomerId" = c."Id"
                );
                """);

            migrationBuilder.DropColumn(
                name: "ConversationStatus",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CurrentIntent",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IntentHistory",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastCustomerMessageAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastIntent",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MemorySummary",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PendingServiceName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TurnCount",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_ConversationStates_CustomerId",
                table: "ConversationStates");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationStates_CustomerId",
                table: "ConversationStates",
                column: "CustomerId",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_CustomerIntentMemories_CustomerId",
                table: "CustomerIntentMemories");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerIntentMemories_CustomerId",
                table: "CustomerIntentMemories",
                column: "CustomerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationStates_CustomerId",
                table: "ConversationStates");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationStates_CustomerId",
                table: "ConversationStates",
                column: "CustomerId");

            migrationBuilder.DropIndex(
                name: "IX_CustomerIntentMemories_CustomerId",
                table: "CustomerIntentMemories");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerIntentMemories_CustomerId",
                table: "CustomerIntentMemories",
                column: "CustomerId");

            migrationBuilder.AddColumn<string>(
                name: "ConversationStatus",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentIntent",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntentHistory",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCustomerMessageAt",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastIntent",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemorySummary",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingServiceName",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TurnCount",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "Hours",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "CurrentIntent",
                table: "ConversationStates");

            migrationBuilder.DropColumn(
                name: "LastIntent",
                table: "ConversationStates");

            migrationBuilder.DropColumn(
                name: "Preferences",
                table: "ConversationStates");

            migrationBuilder.DropColumn(
                name: "TurnCount",
                table: "ConversationStates");

            migrationBuilder.DropColumn(
                name: "LastCustomerMessageAt",
                table: "ConversationStates");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "CustomerIntentMemories");

            migrationBuilder.DropColumn(
                name: "HistoryJson",
                table: "CustomerIntentMemories");
        }
    }
}
