using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_BankAccounts_BankAccountId",
                table: "BankTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsignmentReceipts_Parties_StockOwnerId",
                table: "ConsignmentReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsignmentReceipts_Products_ProductId",
                table: "ConsignmentReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_ConsignmentReceipts_ConsignmentReceiptId",
                table: "SalesInvoices");

            migrationBuilder.RenameColumn(
                name: "StockOwnerPayable",
                table: "SalesInvoices",
                newName: "ProfitAmount");

            migrationBuilder.RenameColumn(
                name: "CommissionRevenue",
                table: "SalesInvoices",
                newName: "ExtraChargesAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "AgentCommissionAmount",
                table: "SalesInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AgentCommissionPercentage",
                table: "SalesInvoices",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BankAccountId",
                table: "SalesInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostOfGoodsSold",
                table: "SalesInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "SalesInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtraChargesDescription",
                table: "SalesInvoices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "SalesInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SalesInvoiceId",
                table: "BankTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_BankAccountId",
                table: "SalesInvoices",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_SalesInvoiceId",
                table: "BankTransactions",
                column: "SalesInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_BankAccounts_BankAccountId",
                table: "BankTransactions",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_SalesInvoices_SalesInvoiceId",
                table: "BankTransactions",
                column: "SalesInvoiceId",
                principalTable: "SalesInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsignmentReceipts_Parties_StockOwnerId",
                table: "ConsignmentReceipts",
                column: "StockOwnerId",
                principalTable: "Parties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsignmentReceipts_Products_ProductId",
                table: "ConsignmentReceipts",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_BankAccounts_BankAccountId",
                table: "SalesInvoices",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_ConsignmentReceipts_ConsignmentReceiptId",
                table: "SalesInvoices",
                column: "ConsignmentReceiptId",
                principalTable: "ConsignmentReceipts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_BankAccounts_BankAccountId",
                table: "BankTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_SalesInvoices_SalesInvoiceId",
                table: "BankTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsignmentReceipts_Parties_StockOwnerId",
                table: "ConsignmentReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsignmentReceipts_Products_ProductId",
                table: "ConsignmentReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_BankAccounts_BankAccountId",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_ConsignmentReceipts_ConsignmentReceiptId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_BankAccountId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_SalesInvoiceId",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "AgentCommissionAmount",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "AgentCommissionPercentage",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "CostOfGoodsSold",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ExtraChargesDescription",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SalesInvoiceId",
                table: "BankTransactions");

            migrationBuilder.RenameColumn(
                name: "ProfitAmount",
                table: "SalesInvoices",
                newName: "StockOwnerPayable");

            migrationBuilder.RenameColumn(
                name: "ExtraChargesAmount",
                table: "SalesInvoices",
                newName: "CommissionRevenue");

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_BankAccounts_BankAccountId",
                table: "BankTransactions",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsignmentReceipts_Parties_StockOwnerId",
                table: "ConsignmentReceipts",
                column: "StockOwnerId",
                principalTable: "Parties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsignmentReceipts_Products_ProductId",
                table: "ConsignmentReceipts",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_ConsignmentReceipts_ConsignmentReceiptId",
                table: "SalesInvoices",
                column: "ConsignmentReceiptId",
                principalTable: "ConsignmentReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
