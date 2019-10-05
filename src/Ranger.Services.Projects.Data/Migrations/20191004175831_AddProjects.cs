using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class AddProjects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(nullable: false),
                    database_username = table.Column<string>(nullable: false),
                    name = table.Column<string>(maxLength: 140, nullable: false),
                    description = table.Column<string>(nullable: true),
                    api_key = table.Column<Guid>(nullable: false),
                    created_at = table.Column<DateTime>(nullable: false),
                    created_by = table.Column<string>(nullable: false),
                    enabled = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_projects_api_key",
                table: "projects",
                column: "api_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_id_name",
                table: "projects",
                columns: new[] { "id", "name" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
