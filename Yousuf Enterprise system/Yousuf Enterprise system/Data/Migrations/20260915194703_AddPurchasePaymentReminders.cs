using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasePaymentReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReminderDismissed",
                table: "OwnedPurchases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OwnedPurchaseId",
                table: "LedgerEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_OwnedPurchaseId",
                table: "LedgerEntries",
                column: "OwnedPurchaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerEntries_OwnedPurchases_OwnedPurchaseId",
                table: "LedgerEntries",
                column: "OwnedPurchaseId",
                principalTable: "OwnedPurchases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerEntries_OwnedPurchases_OwnedPurchaseId",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_OwnedPurchaseId",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "ReminderDismissed",
                table: "OwnedPurchases");

            migrationBuilder.DropColumn(
                name: "OwnedPurchaseId",
                table: "LedgerEntries");
        }
    }
}
