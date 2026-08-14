using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REIGN.Data.Migrations
{
    public partial class FixReignServiceCatalogData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Services
                SET
                    Name = 'QV',
                    Description = 'Quick Visit - less than 30 minutes',
                    Price = 150.00,
                    DurationMinutes = 29,
                    Active = 1,
                    BusinessId = '99999999-9999-9999-9999-999999999999'
                WHERE Id = '11111111-1111-1111-1111-111111111111';

                UPDATE Services
                SET
                    Name = 'HH',
                    Description = 'Half Hour',
                    Price = 300.00,
                    DurationMinutes = 30,
                    Active = 1,
                    BusinessId = '99999999-9999-9999-9999-999999999999'
                WHERE Id = '22222222-2222-2222-2222-222222222222';

                UPDATE Services
                SET
                    Name = 'HR',
                    Description = 'One Hour',
                    Price = 500.00,
                    DurationMinutes = 60,
                    Active = 1,
                    BusinessId = '99999999-9999-9999-9999-999999999999'
                WHERE Id = '33333333-3333-3333-3333-333333333333';

                DELETE FROM Services
                WHERE Id NOT IN (
                    '11111111-1111-1111-1111-111111111111',
                    '22222222-2222-2222-2222-222222222222',
                    '33333333-3333-3333-3333-333333333333'
                );
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Services
                SET
                    Name = 'Oil Change',
                    Description = 'Full synthetic oil service',
                    Price = 89.99,
                    DurationMinutes = 30
                WHERE Id = '11111111-1111-1111-1111-111111111111';

                UPDATE Services
                SET
                    Name = 'Brake Service',
                    Description = 'Brake inspection and repair',
                    Price = 249.99,
                    DurationMinutes = 60
                WHERE Id = '22222222-2222-2222-2222-222222222222';

                UPDATE Services
                SET
                    Name = 'Diagnostic Inspection',
                    Description = 'Complete vehicle diagnostic scan',
                    Price = 129.99,
                    DurationMinutes = 60
                WHERE Id = '33333333-33333333-333333333333';
            """);
        }
    }
}
