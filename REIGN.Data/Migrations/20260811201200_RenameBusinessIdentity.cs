using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REIGN.Data.Migrations
{
    public partial class RenameBusinessIdentity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Businesses",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "Name",
                value: "REIGN AI");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Businesses",
                keyColumn: "Id",
                keyValue: new Guid("99999999-9999-9999-9999-999999999999"),
                column: "Name",
                value: "REIGN Auto Service");
        }
    }
}