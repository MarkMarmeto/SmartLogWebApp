using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorPassSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisitorPasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PassNumber = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QrPayload = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HmacSignature = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QrImageBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CurrentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorPasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VisitorScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitorPassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScanType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorScans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitorScans_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VisitorScans_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitorScans_VisitorPasses_VisitorPassId",
                        column: x => x.VisitorPassId,
                        principalTable: "VisitorPasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorPasses_Code",
                table: "VisitorPasses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitorPasses_CurrentStatus",
                table: "VisitorPasses",
                column: "CurrentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorPasses_PassNumber",
                table: "VisitorPasses",
                column: "PassNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitorScans_AcademicYearId",
                table: "VisitorScans",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorScans_DeviceId",
                table: "VisitorScans",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorScans_ScannedAt",
                table: "VisitorScans",
                column: "ScannedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorScans_Status",
                table: "VisitorScans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorScans_VisitorPassId_ScannedAt",
                table: "VisitorScans",
                columns: new[] { "VisitorPassId", "ScannedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitorScans");

            migrationBuilder.DropTable(
                name: "VisitorPasses");
        }
    }
}
