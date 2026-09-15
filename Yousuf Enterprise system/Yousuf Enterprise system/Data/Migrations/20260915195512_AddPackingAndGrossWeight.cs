using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPackingAndGrossWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old "Bags" count is what PackingCount now represents — rename, not drop, so
            // existing data survives. TotalPackageWeight is a genuinely new column.
            migrationBuilder.RenameColumn(
                name: "Bags",
                table: "OwnedPurchases",
                newName: "PackingCount");

            migrationBuilder.AddColumn<int>(
                name: "TotalPackageWeight",
                table: "OwnedPurchases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossWeight",
                table: "OwnedPurchases",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PackingType",
                table: "OwnedPurchases",
                type: "int",
                nullable: true);

            // Existing rows had a bag count with no type recorded — since it was always called
            // "Bags", tag those as PackingType.Bags (1) so the grid shows "40 Bags" instead of
            // a count with no label.
            migrationBuilder.Sql("UPDATE OwnedPurchases SET PackingType = 1 WHERE PackingCount IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrossWeight",
                table: "OwnedPurchases");

            migrationBuilder.DropColumn(
                name: "PackingType",
                table: "OwnedPurchases");

            migrationBuilder.DropColumn(
                name: "TotalPackageWeight",
                table: "OwnedPurchases");

            migrationBuilder.RenameColumn(
                name: "PackingCount",
                table: "OwnedPurchases",
                newName: "Bags");
        }
    }
}
