using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class AddApiKeyUniqueConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "api_key",
                table: "project_unique_constraints",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_project_unique_constraints_database_username_api_key",
                table: "project_unique_constraints",
                columns: new[] { "database_username", "api_key" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_unique_constraints_database_username_api_key",
                table: "project_unique_constraints");

            migrationBuilder.DropColumn(
                name: "api_key",
                table: "project_unique_constraints");
        }
    }
}
