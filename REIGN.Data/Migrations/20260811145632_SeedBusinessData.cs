using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REIGN.Data.Migrations
{
    public partial class SeedBusinessData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var businessId = new Guid("99999999-9999-9999-9999-999999999999");

            migrationBuilder.InsertData(
                table: "Businesses",
                columns: new[] { "Id", "Name", "Phone", "Address", "Active" },
                values: new object[]
                {
                    businessId,
                    "REIGN Auto Service",
                    "555-0100",
                    "Main Location",
                    true
                });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "BusinessId",
                value: businessId);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "BusinessId",
                value: businessId);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "BusinessId",
                value: businessId);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "BusinessId",
                value: businessId);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Businesses",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"));
        }
    }
}
