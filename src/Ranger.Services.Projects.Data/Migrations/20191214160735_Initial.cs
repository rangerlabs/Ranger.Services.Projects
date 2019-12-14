using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(nullable: true),
                    xml = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_streams",
                columns: table => new
                {
                    id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    database_username = table.Column<string>(nullable: false),
                    stream_id = table.Column<Guid>(nullable: false),
                    version = table.Column<int>(nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    @event = table.Column<string>(name: "event", nullable: false),
                    inserted_at = table.Column<DateTime>(nullable: false),
                    inserted_by = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_streams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_unique_constraints",
                columns: table => new
                {
                    project_id = table.Column<Guid>(nullable: false),
                    database_username = table.Column<string>(nullable: false),
                    hashed_live_api_key = table.Column<string>(nullable: false),
                    hashed_test_api_key = table.Column<string>(nullable: false),
                    name = table.Column<string>(maxLength: 140, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_unique_constraints", x => x.project_id);
                });

            migrationBuilder.CreateTable(
                name: "project_users",
                columns: table => new
                {
                    id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<Guid>(nullable: false),
                    database_username = table.Column<string>(nullable: false),
                    user_id = table.Column<string>(nullable: false),
                    email = table.Column<string>(nullable: false),
                    inserted_at = table.Column<DateTime>(nullable: false),
                    inserted_by = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_unique_constraints_database_username_hashed_live_ap~",
                table: "project_unique_constraints",
                columns: new[] { "database_username", "hashed_live_api_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_unique_constraints_database_username_hashed_test_ap~",
                table: "project_unique_constraints",
                columns: new[] { "database_username", "hashed_test_api_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_unique_constraints_database_username_name",
                table: "project_unique_constraints",
                columns: new[] { "database_username", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_users_project_id_email",
                table: "project_users",
                columns: new[] { "project_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_users_project_id_user_id",
                table: "project_users",
                columns: new[] { "project_id", "user_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "project_streams");

            migrationBuilder.DropTable(
                name: "project_unique_constraints");

            migrationBuilder.DropTable(
                name: "project_users");
        }
    }
}
