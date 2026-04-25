using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraIdentityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CameraIndex",
                table: "VisitorScans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraName",
                table: "VisitorScans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraName",
                table: "Scans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CameraIndex",
                table: "VisitorScans");

            migrationBuilder.DropColumn(
                name: "CameraName",
                table: "VisitorScans");

            migrationBuilder.DropColumn(
                name: "CameraName",
                table: "Scans");
        }
    }
}
