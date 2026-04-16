using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddQrCodeAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QrCodes_StudentId",
                table: "QrCodes");

            migrationBuilder.AddColumn<DateTime>(
                name: "InvalidatedAt",
                table: "QrCodes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedByQrCodeId",
                table: "QrCodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrCodes_ReplacedByQrCodeId",
                table: "QrCodes",
                column: "ReplacedByQrCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_QrCodes_StudentId",
                table: "QrCodes",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_QrCodes_QrCodes_ReplacedByQrCodeId",
                table: "QrCodes",
                column: "ReplacedByQrCodeId",
                principalTable: "QrCodes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QrCodes_QrCodes_ReplacedByQrCodeId",
                table: "QrCodes");

            migrationBuilder.DropIndex(
                name: "IX_QrCodes_ReplacedByQrCodeId",
                table: "QrCodes");

            migrationBuilder.DropIndex(
                name: "IX_QrCodes_StudentId",
                table: "QrCodes");

            migrationBuilder.DropColumn(
                name: "InvalidatedAt",
                table: "QrCodes");

            migrationBuilder.DropColumn(
                name: "ReplacedByQrCodeId",
                table: "QrCodes");

            migrationBuilder.CreateIndex(
                name: "IX_QrCodes_StudentId",
                table: "QrCodes",
                column: "StudentId",
                unique: true);
        }
    }
}
