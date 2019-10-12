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
                name: "project_streams",
                columns: table => new
                {
                    id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    project_id = table.Column<Guid>(nullable: false),
                    database_username = table.Column<string>(nullable: false),
                    stream_id = table.Column<Guid>(nullable: false),
                    domain = table.Column<string>(maxLength: 28, nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_project_streams_domain_project_id",
                table: "project_streams",
                columns: new[] { "domain", "project_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_streams_project_id_version",
                table: "project_streams",
                columns: new[] { "project_id", "version" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "project_streams");
        }
    }
}
