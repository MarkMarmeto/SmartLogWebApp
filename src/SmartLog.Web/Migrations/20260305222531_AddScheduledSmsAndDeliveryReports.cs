using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledSmsAndDeliveryReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "SmsQueues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "SmsLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "SmsLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                table: "SmsLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsQueues_ScheduledAt",
                table: "SmsQueues",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_ProviderMessageId",
                table: "SmsLogs",
                column: "ProviderMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmsQueues_ScheduledAt",
                table: "SmsQueues");

            migrationBuilder.DropIndex(
                name: "IX_SmsLogs_ProviderMessageId",
                table: "SmsLogs");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "SmsQueues");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "SmsLogs");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "SmsLogs");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "SmsLogs");
        }
    }
}
