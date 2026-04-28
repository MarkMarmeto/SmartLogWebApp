using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceHeartbeatSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppVersion",
                table: "Devices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatteryPercent",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCharging",
                table: "Devices",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatAt",
                table: "Devices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NetworkType",
                table: "Devices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsVersion",
                table: "Devices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QueuedScansCount",
                table: "Devices",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BatteryPercent",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsCharging",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NetworkType",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "OsVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "QueuedScansCount",
                table: "Devices");
        }
    }
}
