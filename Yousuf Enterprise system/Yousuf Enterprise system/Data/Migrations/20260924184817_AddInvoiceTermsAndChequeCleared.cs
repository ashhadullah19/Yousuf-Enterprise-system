using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTermsAndChequeCleared : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TermsAndConditions",
                table: "SalesInvoices",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChequeCleared",
                table: "LedgerEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TermsAndConditions",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ChequeCleared",
                table: "LedgerEntries");
        }
    }
}
