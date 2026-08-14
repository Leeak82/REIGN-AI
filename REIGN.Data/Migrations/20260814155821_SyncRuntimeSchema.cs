using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace REIGN.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncRuntimeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ServiceRecommendations",
                keyColumn: "Id",
                keyValue: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            migrationBuilder.DeleteData(
                table: "ServiceRecommendations",
                keyColumn: "Id",
                keyValue: new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            migrationBuilder.DeleteData(
                table: "ServiceRecommendations",
                keyColumn: "Id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"));

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

            migrationBuilder.AddColumn<bool>(
                name: "HumanOverrideActive",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "HumanOverrideAt",
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

            migrationBuilder.AddColumn<bool>(
                name: "IsOwnerOverride",
                table: "ConversationMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ConversationMessages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalCalendarEventId",
                table: "Appointments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntegrationTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TokenType = table.Column<string>(type: "TEXT", nullable: true),
                    Scope = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationTokens", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "Id", "Active", "DurationMinutes", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("9c1a1111-1111-4111-8111-111111111111"), true, 20, "Quick Visit", 150m },
                    { new Guid("9c1a2222-2222-4222-8222-222222222222"), true, 30, "Half Hour", 300m },
                    { new Guid("9c1a3333-3333-4333-8333-333333333333"), true, 60, "Hour", 500m }
                });

            migrationBuilder.InsertData(
                table: "ServiceRecommendations",
                columns: new[] { "Id", "Active", "Recommendation", "ServiceId", "Trigger" },
                values: new object[,]
                {
                    { new Guid("9c1aaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), true, "Customer is asking about a Quick Visit (QV): $150, less than 30 minutes.", new Guid("9c1a1111-1111-4111-8111-111111111111"), "quick" },
                    { new Guid("9c1bbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"), true, "Customer is asking about a Half Hour appointment (HH): $300, 30 minutes.", new Guid("9c1a2222-2222-4222-8222-222222222222"), "half" },
                    { new Guid("9c1ccccc-cccc-4ccc-8ccc-cccccccccccc"), true, "Customer is asking about an Hour appointment (HR): $500, 60 minutes.", new Guid("9c1a3333-3333-4333-8333-333333333333"), "hour" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationTokens_Provider",
                table: "IntegrationTokens",
                column: "Provider",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationTokens");

            migrationBuilder.DeleteData(
                table: "ServiceRecommendations",
                keyColumn: "Id",
                keyValue: new Guid("9c1aaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            migrationBuilder.DeleteData(
                table: "ServiceRecommendations",
                keyColumn: "Id",
                keyValue: new Guid("9c1bbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));

            migrationBuilder.DeleteData(
                table: "ServiceRecommendations",
                keyColumn: "Id",
                keyValue: new Guid("9c1ccccc-cccc-4ccc-8ccc-cccccccccccc"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("9c1a1111-1111-4111-8111-111111111111"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("9c1a2222-2222-4222-8222-222222222222"));

            migrationBuilder.DeleteData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("9c1a3333-3333-4333-8333-333333333333"));

            migrationBuilder.DropColumn(
                name: "ConversationStatus",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CurrentIntent",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "HumanOverrideActive",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "HumanOverrideAt",
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

            migrationBuilder.DropColumn(
                name: "IsOwnerOverride",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "ExternalCalendarEventId",
                table: "Appointments");

            migrationBuilder.InsertData(
                table: "Services",
                columns: new[] { "Id", "Active", "DurationMinutes", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), true, 30, "Oil Change", 89.99m },
                    { new Guid("22222222-2222-2222-2222-222222222222"), true, 60, "Brake Service", 249.99m },
                    { new Guid("33333333-3333-3333-3333-333333333333"), true, 60, "Diagnostic Inspection", 129.99m },
                    { new Guid("44444444-4444-4444-4444-444444444444"), true, 30, "Vehicle Inspection", 79.99m }
                });

            migrationBuilder.InsertData(
                table: "ServiceRecommendations",
                columns: new[] { "Id", "Active", "Recommendation", "ServiceId", "Trigger" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), true, "Customer likely needs routine oil maintenance.", new Guid("11111111-1111-1111-1111-111111111111"), "oil" },
                    { new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), true, "Customer may need brake service.", new Guid("22222222-2222-2222-2222-222222222222"), "brake" },
                    { new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"), true, "Customer requires diagnostic inspection.", new Guid("33333333-3333-3333-3333-333333333333"), "diagnostic" }
                });
        }
    }
}
