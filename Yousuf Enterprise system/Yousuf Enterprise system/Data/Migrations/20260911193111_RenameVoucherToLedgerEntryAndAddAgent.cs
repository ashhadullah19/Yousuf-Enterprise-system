using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameVoucherToLedgerEntryAndAddAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renaming (not drop+recreate) so any existing rows survive.
            migrationBuilder.RenameTable(
                name: "FinancialVouchers",
                newName: "LedgerEntries");

            migrationBuilder.RenameColumn(
                name: "VoucherNumber",
                table: "LedgerEntries",
                newName: "LedgerNumber");

            migrationBuilder.RenameColumn(
                name: "VoucherDate",
                table: "LedgerEntries",
                newName: "LedgerDate");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialVouchers_PartyId",
                table: "LedgerEntries",
                newName: "IX_LedgerEntries_PartyId");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialVouchers_SalesInvoiceId",
                table: "LedgerEntries",
                newName: "IX_LedgerEntries_SalesInvoiceId");

            migrationBuilder.AddColumn<int>(
                name: "AgentId",
                table: "SalesInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_AgentId",
                table: "SalesInvoices",
                column: "AgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Parties_AgentId",
                table: "SalesInvoices",
                column: "AgentId",
                principalTable: "Parties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Parties_AgentId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_AgentId",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "SalesInvoices");

            migrationBuilder.RenameIndex(
                name: "IX_LedgerEntries_PartyId",
                table: "LedgerEntries",
                newName: "IX_FinancialVouchers_PartyId");

            migrationBuilder.RenameIndex(
                name: "IX_LedgerEntries_SalesInvoiceId",
                table: "LedgerEntries",
                newName: "IX_FinancialVouchers_SalesInvoiceId");

            migrationBuilder.RenameColumn(
                name: "LedgerNumber",
                table: "LedgerEntries",
                newName: "VoucherNumber");

            migrationBuilder.RenameColumn(
                name: "LedgerDate",
                table: "LedgerEntries",
                newName: "VoucherDate");

            migrationBuilder.RenameTable(
                name: "LedgerEntries",
                newName: "FinancialVouchers");
        }
    }
}
