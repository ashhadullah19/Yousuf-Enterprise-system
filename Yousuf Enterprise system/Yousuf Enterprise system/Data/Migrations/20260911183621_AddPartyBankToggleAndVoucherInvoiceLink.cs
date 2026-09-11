using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyBankToggleAndVoucherInvoiceLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_SalesInvoices_SalesInvoiceId",
                table: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_SalesInvoiceId",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "SalesInvoiceId",
                table: "BankTransactions");

            // Backfill any pre-existing NULLs so the NOT NULL alter below can't fail on old data.
            migrationBuilder.Sql("UPDATE Parties SET PrimaryContact = '' WHERE PrimaryContact IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryContact",
                table: "Parties",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBankDetails",
                table: "Parties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SalesInvoiceId",
                table: "FinancialVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialVouchers_SalesInvoiceId",
                table: "FinancialVouchers",
                column: "SalesInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialVouchers_SalesInvoices_SalesInvoiceId",
                table: "FinancialVouchers",
                column: "SalesInvoiceId",
                principalTable: "SalesInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialVouchers_SalesInvoices_SalesInvoiceId",
                table: "FinancialVouchers");

            migrationBuilder.DropIndex(
                name: "IX_FinancialVouchers_SalesInvoiceId",
                table: "FinancialVouchers");

            migrationBuilder.DropColumn(
                name: "HasBankDetails",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "SalesInvoiceId",
                table: "FinancialVouchers");

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryContact",
                table: "Parties",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<int>(
                name: "SalesInvoiceId",
                table: "BankTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_SalesInvoiceId",
                table: "BankTransactions",
                column: "SalesInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_SalesInvoices_SalesInvoiceId",
                table: "BankTransactions",
                column: "SalesInvoiceId",
                principalTable: "SalesInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
