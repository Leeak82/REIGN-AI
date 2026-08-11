using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REIGN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationMessage_Customers_CustomerId",
                table: "ConversationMessage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConversationMessage",
                table: "ConversationMessage");

            migrationBuilder.RenameTable(
                name: "ConversationMessage",
                newName: "ConversationMessages");

            migrationBuilder.RenameIndex(
                name: "IX_ConversationMessage_CustomerId",
                table: "ConversationMessages",
                newName: "IX_ConversationMessages_CustomerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConversationMessages",
                table: "ConversationMessages",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationMessages_Customers_CustomerId",
                table: "ConversationMessages",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationMessages_Customers_CustomerId",
                table: "ConversationMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConversationMessages",
                table: "ConversationMessages");

            migrationBuilder.RenameTable(
                name: "ConversationMessages",
                newName: "ConversationMessage");

            migrationBuilder.RenameIndex(
                name: "IX_ConversationMessages_CustomerId",
                table: "ConversationMessage",
                newName: "IX_ConversationMessage_CustomerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConversationMessage",
                table: "ConversationMessage",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationMessage_Customers_CustomerId",
                table: "ConversationMessage",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
