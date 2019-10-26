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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    friendly_name = table.Column<string>(nullable: true),
                    xml = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_unique_constraints",
                columns: table => new
                {
                    project_id = table.Column<Guid>(nullable: false),
                    hashed_live_api_key = table.Column<string>(nullable: false),
                    hashed_test_api_key = table.Column<string>(nullable: false),
                    name = table.Column<string>(maxLength: 140, nullable: false),
                    database_username = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_unique_constraints", x => x.project_id);
                });

            migrationBuilder.CreateTable(
                name: "project_streams",
                columns: table => new
                {
                    id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    database_username = table.Column<string>(nullable: false),
                    stream_id = table.Column<Guid>(nullable: false),
                    project_unique_constraint_project_id = table.Column<Guid>(nullable: false),
                    version = table.Column<int>(nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    @event = table.Column<string>(name: "event", nullable: false),
                    inserted_at = table.Column<DateTime>(nullable: false),
                    inserted_by = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_streams", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_streams_project_unique_constraints_project_unique_con~",
                        column: x => x.project_unique_constraint_project_id,
                        principalTable: "project_unique_constraints",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_streams_project_unique_constraint_project_id",
                table: "project_streams",
                column: "project_unique_constraint_project_id");

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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "project_streams");

            migrationBuilder.DropTable(
                name: "project_unique_constraints");
        }
    }
}
