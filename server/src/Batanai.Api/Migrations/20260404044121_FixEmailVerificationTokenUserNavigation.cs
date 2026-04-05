using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixEmailVerificationTokenUserNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_email_verification_tokens_users_user_id1",
                table: "EmailVerificationTokens");

            migrationBuilder.DropIndex(
                name: "ix_email_verification_tokens_user_id1",
                table: "EmailVerificationTokens");

            migrationBuilder.DropColumn(
                name: "user_id1",
                table: "EmailVerificationTokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "user_id1",
                table: "EmailVerificationTokens",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_user_id1",
                table: "EmailVerificationTokens",
                column: "user_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_email_verification_tokens_users_user_id1",
                table: "EmailVerificationTokens",
                column: "user_id1",
                principalTable: "Users",
                principalColumn: "id");
        }
    }
}
