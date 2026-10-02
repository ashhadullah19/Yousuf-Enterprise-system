using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesInvoicePackageAndGrossWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GrossWeight",
                table: "SalesInvoices",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalPackageWeight",
                table: "SalesInvoices",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrossWeight",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "TotalPackageWeight",
                table: "SalesInvoices");
        }
    }
}
