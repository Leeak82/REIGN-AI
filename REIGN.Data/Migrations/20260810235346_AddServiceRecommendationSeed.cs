using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace REIGN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRecommendationSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
