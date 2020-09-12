using Microsoft.EntityFrameworkCore.Migrations;

namespace Ranger.Services.Projects.Data.Migrations
{
    public partial class OutboxTableCorrections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_outbox_ranger_rabbit_message_message_id",
                table: "outbox");

            migrationBuilder.DropPrimaryKey(
                name: "pk_ranger_rabbit_message",
                table: "ranger_rabbit_message");

            migrationBuilder.DropPrimaryKey(
                name: "pk_outbox",
                table: "outbox");

            migrationBuilder.RenameTable(
                name: "ranger_rabbit_message",
                newName: "ranger_rabbit_messages");

            migrationBuilder.RenameTable(
                name: "outbox",
                newName: "outbox_messages");

            migrationBuilder.RenameIndex(
                name: "ix_outbox_message_id",
                table: "outbox_messages",
                newName: "ix_outbox_messages_message_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_ranger_rabbit_messages",
                table: "ranger_rabbit_messages",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_outbox_messages",
                table: "outbox_messages",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_outbox_messages_ranger_rabbit_messages_message_id",
                table: "outbox_messages",
                column: "message_id",
                principalTable: "ranger_rabbit_messages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_outbox_messages_ranger_rabbit_messages_message_id",
                table: "outbox_messages");

            migrationBuilder.DropPrimaryKey(
                name: "pk_ranger_rabbit_messages",
                table: "ranger_rabbit_messages");

            migrationBuilder.DropPrimaryKey(
                name: "pk_outbox_messages",
                table: "outbox_messages");

            migrationBuilder.RenameTable(
                name: "ranger_rabbit_messages",
                newName: "ranger_rabbit_message");

            migrationBuilder.RenameTable(
                name: "outbox_messages",
                newName: "outbox");

            migrationBuilder.RenameIndex(
                name: "ix_outbox_messages_message_id",
                table: "outbox",
                newName: "ix_outbox_message_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_ranger_rabbit_message",
                table: "ranger_rabbit_message",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_outbox",
                table: "outbox",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_outbox_ranger_rabbit_message_message_id",
                table: "outbox",
                column: "message_id",
                principalTable: "ranger_rabbit_message",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
