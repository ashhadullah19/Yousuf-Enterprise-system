using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesInvoicePackingTypeAndCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PackingCount",
                table: "SalesInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PackingType",
                table: "SalesInvoices",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackingCount",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PackingType",
                table: "SalesInvoices");
        }
    }
}
