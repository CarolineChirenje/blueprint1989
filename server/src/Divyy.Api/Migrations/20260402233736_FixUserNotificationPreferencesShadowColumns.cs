using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divvy.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixUserNotificationPreferencesShadowColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_notification_preferences_notification_types_notificati~",
                table: "UserNotificationPreferences");

            migrationBuilder.DropForeignKey(
                name: "fk_user_notification_preferences_users_user_id1",
                table: "UserNotificationPreferences");

            migrationBuilder.DropIndex(
                name: "ix_user_notification_preferences_notification_type_id1",
                table: "UserNotificationPreferences");

            migrationBuilder.DropIndex(
                name: "ix_user_notification_preferences_user_id1",
                table: "UserNotificationPreferences");

            migrationBuilder.DropColumn(
                name: "notification_type_id1",
                table: "UserNotificationPreferences");

            migrationBuilder.DropColumn(
                name: "user_id1",
                table: "UserNotificationPreferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "notification_type_id1",
                table: "UserNotificationPreferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "user_id1",
                table: "UserNotificationPreferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_user_notification_preferences_notification_type_id1",
                table: "UserNotificationPreferences",
                column: "notification_type_id1");

            migrationBuilder.CreateIndex(
                name: "ix_user_notification_preferences_user_id1",
                table: "UserNotificationPreferences",
                column: "user_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_user_notification_preferences_notification_types_notificati~",
                table: "UserNotificationPreferences",
                column: "notification_type_id1",
                principalTable: "NotificationTypes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_notification_preferences_users_user_id1",
                table: "UserNotificationPreferences",
                column: "user_id1",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
