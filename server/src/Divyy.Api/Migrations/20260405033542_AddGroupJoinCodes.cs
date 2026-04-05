using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Divvy.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupJoinCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "join_code",
                table: "Groups",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "join_code_generated_at",
                table: "Groups",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Generate unique join codes for existing groups
            migrationBuilder.Sql(@"
                UPDATE ""Groups""
                SET join_code = UPPER(SUBSTRING(MD5(RANDOM()::text || id::text) FROM 1 FOR 8)),
                    join_code_generated_at = NOW() AT TIME ZONE 'UTC'
                WHERE join_code = '';
            ");

            migrationBuilder.AlterColumn<int>(
                name: "invited_by_user_id",
                table: "GroupMembers",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "GroupMembers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "approved_by_user_id",
                table: "GroupMembers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "join_requested_at",
                table: "GroupMembers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_groups_join_code",
                table: "Groups",
                column: "join_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_members_approved_by_user_id",
                table: "GroupMembers",
                column: "approved_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_group_members_users_approved_by_user_id",
                table: "GroupMembers",
                column: "approved_by_user_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_group_members_users_approved_by_user_id",
                table: "GroupMembers");

            migrationBuilder.DropIndex(
                name: "ix_groups_join_code",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "ix_group_members_approved_by_user_id",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "join_code",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "join_code_generated_at",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "approved_by_user_id",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "join_requested_at",
                table: "GroupMembers");

            migrationBuilder.AlterColumn<int>(
                name: "invited_by_user_id",
                table: "GroupMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
