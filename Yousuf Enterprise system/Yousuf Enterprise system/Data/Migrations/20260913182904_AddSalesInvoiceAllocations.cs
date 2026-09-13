using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesInvoiceAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesInvoiceAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesInvoiceId = table.Column<int>(type: "int", nullable: false),
                    OwnedPurchaseId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PurchaseRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CommissionPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoiceAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceAllocations_OwnedPurchases_OwnedPurchaseId",
                        column: x => x.OwnedPurchaseId,
                        principalTable: "OwnedPurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesInvoiceAllocations_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceAllocations_OwnedPurchaseId",
                table: "SalesInvoiceAllocations",
                column: "OwnedPurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoiceAllocations_SalesInvoiceId",
                table: "SalesInvoiceAllocations",
                column: "SalesInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesInvoiceAllocations");
        }
    }
}
