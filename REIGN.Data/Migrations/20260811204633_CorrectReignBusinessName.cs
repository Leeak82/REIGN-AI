using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REIGN.Data.Migrations
{
    public partial class CorrectReignBusinessName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE Businesses
                SET Name = 'REIGN AI'
                WHERE Id = '99999999-9999-9999-9999-999999999999';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE Businesses
                SET Name = 'REIGN Auto Service'
                WHERE Id = '99999999-9999-9999-9999-999999999999';
            ");
        }
    }
}
