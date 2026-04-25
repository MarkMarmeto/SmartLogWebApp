using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLog.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProgramHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Programs_Programs_ParentProgramId",
                table: "Programs");

            migrationBuilder.DropIndex(
                name: "IX_Programs_ParentProgramId",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "ParentProgramId",
                table: "Programs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentProgramId",
                table: "Programs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Programs_ParentProgramId",
                table: "Programs",
                column: "ParentProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_Programs_ParentProgramId",
                table: "Programs",
                column: "ParentProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
