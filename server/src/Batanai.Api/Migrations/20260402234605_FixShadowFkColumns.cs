using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixShadowFkColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_users_user_id1",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "fk_push_subscriptions_users_user_id1",
                table: "PushSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "fk_user_devices_users_user_id1",
                table: "UserDevices");

            migrationBuilder.DropIndex(
                name: "ix_user_devices_user_id1",
                table: "UserDevices");

            migrationBuilder.DropIndex(
                name: "ix_push_subscriptions_user_id1",
                table: "PushSubscriptions");

            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id1",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "user_id1",
                table: "UserDevices");

            migrationBuilder.DropColumn(
                name: "user_id1",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "user_id1",
                table: "Notifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "user_id1",
                table: "UserDevices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "user_id1",
                table: "PushSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "user_id1",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_user_devices_user_id1",
                table: "UserDevices",
                column: "user_id1");

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_user_id1",
                table: "PushSubscriptions",
                column: "user_id1");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id1",
                table: "Notifications",
                column: "user_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_users_user_id1",
                table: "Notifications",
                column: "user_id1",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_push_subscriptions_users_user_id1",
                table: "PushSubscriptions",
                column: "user_id1",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_devices_users_user_id1",
                table: "UserDevices",
                column: "user_id1",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
