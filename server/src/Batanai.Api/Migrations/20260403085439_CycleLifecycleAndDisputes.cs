using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class CycleLifecycleAndDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_expenses_users_paid_by_user_id",
                table: "Expenses");

            migrationBuilder.RenameColumn(
                name: "paid_by_user_id",
                table: "Expenses",
                newName: "logged_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_expenses_paid_by_user_id",
                table: "Expenses",
                newName: "ix_expenses_logged_by_user_id");

            migrationBuilder.AddColumn<bool>(
                name: "closing_soon_sent",
                table: "ExpenseCycles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "mid_reminder_sent",
                table: "ExpenseCycles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "split_type",
                table: "ExpenseCycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "start_notification_sent",
                table: "ExpenseCycles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "share_percentage",
                table: "CycleMembers",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpenseDisputes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_id = table.Column<int>(type: "integer", nullable: false),
                    raised_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    admin_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_disputes", x => x.id);
                    table.ForeignKey(
                        name: "fk_expense_disputes_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "Expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_expense_disputes_users_raised_by_user_id",
                        column: x => x.raised_by_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expense_disputes_expense_id",
                table: "ExpenseDisputes",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_disputes_raised_by_user_id",
                table: "ExpenseDisputes",
                column: "raised_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_expenses_users_logged_by_user_id",
                table: "Expenses",
                column: "logged_by_user_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_expenses_users_logged_by_user_id",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "ExpenseDisputes");

            migrationBuilder.DropColumn(
                name: "closing_soon_sent",
                table: "ExpenseCycles");

            migrationBuilder.DropColumn(
                name: "mid_reminder_sent",
                table: "ExpenseCycles");

            migrationBuilder.DropColumn(
                name: "split_type",
                table: "ExpenseCycles");

            migrationBuilder.DropColumn(
                name: "start_notification_sent",
                table: "ExpenseCycles");

            migrationBuilder.DropColumn(
                name: "share_percentage",
                table: "CycleMembers");

            migrationBuilder.RenameColumn(
                name: "logged_by_user_id",
                table: "Expenses",
                newName: "paid_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_expenses_logged_by_user_id",
                table: "Expenses",
                newName: "ix_expenses_paid_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_expenses_users_paid_by_user_id",
                table: "Expenses",
                column: "paid_by_user_id",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
