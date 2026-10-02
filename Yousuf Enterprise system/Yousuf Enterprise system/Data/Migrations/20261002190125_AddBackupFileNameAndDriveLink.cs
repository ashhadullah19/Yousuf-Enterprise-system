using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yousuf_Enterprise_system.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupFileNameAndDriveLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveFolderLink",
                table: "SystemSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "BackupLogs",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleDriveFolderLink",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "BackupLogs");
        }
    }
}
