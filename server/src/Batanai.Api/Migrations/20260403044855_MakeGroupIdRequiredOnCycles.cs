using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeGroupIdRequiredOnCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_expense_cycles_groups_group_id",
                table: "ExpenseCycles");

            migrationBuilder.AlterColumn<int>(
                name: "group_id",
                table: "ExpenseCycles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_expense_cycles_groups_group_id",
                table: "ExpenseCycles",
                column: "group_id",
                principalTable: "Groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_expense_cycles_groups_group_id",
                table: "ExpenseCycles");

            migrationBuilder.AlterColumn<int>(
                name: "group_id",
                table: "ExpenseCycles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "fk_expense_cycles_groups_group_id",
                table: "ExpenseCycles",
                column: "group_id",
                principalTable: "Groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
