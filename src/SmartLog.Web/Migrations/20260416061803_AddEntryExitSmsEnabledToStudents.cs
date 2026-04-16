using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryExitSmsEnabledToStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scans_StudentId",
                table: "Scans");

            migrationBuilder.AddColumn<bool>(
                name: "EntryExitSmsEnabled",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Scans_NoDuplicateAccepted",
                table: "Scans",
                columns: new[] { "StudentId", "ScanType", "ScannedAt" },
                unique: true,
                filter: "[Status] = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "IX_Scans_Status_ScannedAt",
                table: "Scans",
                columns: new[] { "Status", "ScannedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scans_NoDuplicateAccepted",
                table: "Scans");

            migrationBuilder.DropIndex(
                name: "IX_Scans_Status_ScannedAt",
                table: "Scans");

            migrationBuilder.DropColumn(
                name: "EntryExitSmsEnabled",
                table: "Students");

            migrationBuilder.CreateIndex(
                name: "IX_Scans_StudentId",
                table: "Scans",
                column: "StudentId");
        }
    }
}
