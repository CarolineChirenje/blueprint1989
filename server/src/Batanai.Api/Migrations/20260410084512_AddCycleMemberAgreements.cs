using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleMemberAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CycleMemberAgreements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_cycle_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    agreed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cycle_member_agreements", x => x.id);
                    table.ForeignKey(
                        name: "fk_cycle_member_agreements_expense_cycles_expense_cycle_id",
                        column: x => x.expense_cycle_id,
                        principalTable: "ExpenseCycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cycle_member_agreements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cycle_member_agreements_expense_cycle_id_user_id",
                table: "CycleMemberAgreements",
                columns: new[] { "expense_cycle_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cycle_member_agreements_user_id",
                table: "CycleMemberAgreements",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CycleMemberAgreements");
        }
    }
}
