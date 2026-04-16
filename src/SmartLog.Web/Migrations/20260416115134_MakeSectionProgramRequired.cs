using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class MakeSectionProgramRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // US0065 data migration: assign REGULAR program to all sections with null ProgramId
            migrationBuilder.Sql(@"
                UPDATE Sections
                SET ProgramId = (SELECT TOP 1 Id FROM Programs WHERE Code = 'REGULAR')
                WHERE ProgramId IS NULL
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProgramId",
                table: "Sections",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ProgramId",
                table: "Sections",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}
