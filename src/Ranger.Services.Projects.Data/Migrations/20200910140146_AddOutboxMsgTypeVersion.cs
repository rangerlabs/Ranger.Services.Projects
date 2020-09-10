using Microsoft.EntityFrameworkCore.Migrations;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class AddOutboxMsgTypeVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "message_version",
                table: "ranger_rabbit_message",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "ranger_rabbit_message",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "message_version",
                table: "ranger_rabbit_message");

            migrationBuilder.DropColumn(
                name: "type",
                table: "ranger_rabbit_message");
        }
    }
}
