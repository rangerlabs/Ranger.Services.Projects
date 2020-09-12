using Microsoft.EntityFrameworkCore.Migrations;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class OutboxOneToOneRelationship : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_message_id",
                table: "outbox");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_id",
                table: "outbox",
                column: "message_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_message_id",
                table: "outbox");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_id",
                table: "outbox",
                column: "message_id");
        }
    }
}
