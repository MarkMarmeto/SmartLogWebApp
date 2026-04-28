using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleAuditLogFromAspNetUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_AspNetUsers_PerformedByUserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_AspNetUsers_UserId",
                table: "AuditLogs");

            migrationBuilder.AddColumn<string>(
                name: "PerformedByUserName",
                table: "AuditLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "AuditLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            // Backfill snapshot columns from current AspNetUsers.
            // Rows whose referenced user no longer exists are left NULL — viewer falls back to id.
            migrationBuilder.Sql(@"
                UPDATE a SET a.UserName = u.UserName
                FROM AuditLogs a
                INNER JOIN AspNetUsers u ON a.UserId = u.Id
                WHERE a.UserName IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE a SET a.PerformedByUserName = u.UserName
                FROM AuditLogs a
                INNER JOIN AspNetUsers u ON a.PerformedByUserId = u.Id
                WHERE a.PerformedByUserName IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerformedByUserName",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "AuditLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_AspNetUsers_PerformedByUserId",
                table: "AuditLogs",
                column: "PerformedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_AspNetUsers_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
