using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseTypes_Name",
                table: "ExpenseTypes",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpenseTypeId",
                table: "Expenses",
                type: "int",
                nullable: true);

            // Carry every free-text category already typed into an expense over as a type,
            // plus the suggestions the old form offered, so nothing is lost and the dropdown isn't empty.
            migrationBuilder.Sql(@"
INSERT INTO ExpenseTypes (Name, IsActive)
SELECT MIN(LTRIM(RTRIM(Category))), 1
FROM Expenses
WHERE LTRIM(RTRIM(Category)) <> ''
GROUP BY LTRIM(RTRIM(Category));");

            migrationBuilder.Sql(@"
INSERT INTO ExpenseTypes (Name, IsActive)
SELECT v.Name, 1
FROM (VALUES ('Conveyance'), ('Labour'), ('Rent'), ('Utilities'), ('Office Supplies'), ('Miscellaneous')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM ExpenseTypes t WHERE t.Name = v.Name);");

            migrationBuilder.Sql(@"
UPDATE e SET e.ExpenseTypeId = t.Id
FROM Expenses e
JOIN ExpenseTypes t ON t.Name = LTRIM(RTRIM(e.Category));");

            migrationBuilder.Sql(@"
UPDATE Expenses SET ExpenseTypeId = (SELECT Id FROM ExpenseTypes WHERE Name = 'Miscellaneous')
WHERE ExpenseTypeId IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "ExpenseTypeId",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseTypeId",
                table: "Expenses",
                column: "ExpenseTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_ExpenseTypes_ExpenseTypeId",
                table: "Expenses",
                column: "ExpenseTypeId",
                principalTable: "ExpenseTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Expenses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE e SET e.Category = t.Name
FROM Expenses e
JOIN ExpenseTypes t ON t.Id = e.ExpenseTypeId;");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_ExpenseTypes_ExpenseTypeId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "ExpenseTypes");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ExpenseTypeId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ExpenseTypeId",
                table: "Expenses");
        }
    }
}
