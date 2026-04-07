using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BroadcastId",
                table: "SmsQueues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Broadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AffectedGrades = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalRecipients = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Broadcasts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsQueues_BroadcastId",
                table: "SmsQueues",
                column: "BroadcastId");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_CreatedAt",
                table: "Broadcasts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_ScheduledAt",
                table: "Broadcasts",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_Status",
                table: "Broadcasts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_Type",
                table: "Broadcasts",
                column: "Type");

            migrationBuilder.AddForeignKey(
                name: "FK_SmsQueues_Broadcasts_BroadcastId",
                table: "SmsQueues",
                column: "BroadcastId",
                principalTable: "Broadcasts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmsQueues_Broadcasts_BroadcastId",
                table: "SmsQueues");

            migrationBuilder.DropTable(
                name: "Broadcasts");

            migrationBuilder.DropIndex(
                name: "IX_SmsQueues_BroadcastId",
                table: "SmsQueues");

            migrationBuilder.DropColumn(
                name: "BroadcastId",
                table: "SmsQueues");
        }
    }
}
