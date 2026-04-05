using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMukandoCycleType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "contribution_amount",
                table: "ExpenseCycles",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "currency_id",
                table: "ExpenseCycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cycle_type",
                table: "ExpenseCycles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "frequency",
                table: "ExpenseCycles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    symbol = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currencies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MukandoOptOutRequests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_cycle_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    responded_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mukando_opt_out_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_mukando_opt_out_requests_expense_cycles_expense_cycle_id",
                        column: x => x.expense_cycle_id,
                        principalTable: "ExpenseCycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mukando_opt_out_requests_users_responded_by_user_id",
                        column: x => x.responded_by_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mukando_opt_out_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MukandoRounds",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_cycle_id = table.Column<int>(type: "integer", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    recipient_user_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expected_pool = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    actual_collected = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    payout_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    payout_confirmed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mukando_rounds", x => x.id);
                    table.ForeignKey(
                        name: "fk_mukando_rounds_expense_cycles_expense_cycle_id",
                        column: x => x.expense_cycle_id,
                        principalTable: "ExpenseCycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mukando_rounds_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MukandoContributions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mukando_round_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    proof_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    confirmed_by_admin_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reminder1sent = table.Column<bool>(type: "boolean", nullable: false),
                    reminder2sent = table.Column<bool>(type: "boolean", nullable: false),
                    escalation_sent = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mukando_contributions", x => x.id);
                    table.ForeignKey(
                        name: "fk_mukando_contributions_mukando_rounds_mukando_round_id",
                        column: x => x.mukando_round_id,
                        principalTable: "MukandoRounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mukando_contributions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MukandoPayouts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mukando_round_id = table.Column<int>(type: "integer", nullable: false),
                    recipient_user_id = table.Column<int>(type: "integer", nullable: false),
                    amount_disbursed = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    payment_method = table.Column<int>(type: "integer", nullable: false),
                    proof_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    confirmed_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mukando_payouts", x => x.id);
                    table.ForeignKey(
                        name: "fk_mukando_payouts_mukando_rounds_mukando_round_id",
                        column: x => x.mukando_round_id,
                        principalTable: "MukandoRounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mukando_payouts_users_confirmed_by_user_id",
                        column: x => x.confirmed_by_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mukando_payouts_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MukandoRoundActivities",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mukando_round_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mukando_round_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_mukando_round_activities_mukando_rounds_mukando_round_id",
                        column: x => x.mukando_round_id,
                        principalTable: "MukandoRounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mukando_round_activities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MukandoSwapRequests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_cycle_id = table.Column<int>(type: "integer", nullable: false),
                    requester_user_id = table.Column<int>(type: "integer", nullable: false),
                    requester_round_id = table.Column<int>(type: "integer", nullable: false),
                    target_user_id = table.Column<int>(type: "integer", nullable: false),
                    target_round_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mukando_swap_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_mukando_swap_requests_expense_cycles_expense_cycle_id",
                        column: x => x.expense_cycle_id,
                        principalTable: "ExpenseCycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mukando_swap_requests_mukando_rounds_requester_round_id",
                        column: x => x.requester_round_id,
                        principalTable: "MukandoRounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mukando_swap_requests_mukando_rounds_target_round_id",
                        column: x => x.target_round_id,
                        principalTable: "MukandoRounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mukando_swap_requests_users_requester_user_id",
                        column: x => x.requester_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mukando_swap_requests_users_target_user_id",
                        column: x => x.target_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expense_cycles_currency_id",
                table: "ExpenseCycles",
                column: "currency_id");

            migrationBuilder.CreateIndex(
                name: "ix_currencies_code",
                table: "Currencies",
                column: "code",
                unique: true);

            // Seed a default currency so existing rows can reference it
            migrationBuilder.Sql(@"
                INSERT INTO ""Currencies"" (code, name, symbol, is_active)
                VALUES ('USD', 'US Dollar', '$', true);
            ");
            migrationBuilder.Sql(@"
                UPDATE ""ExpenseCycles"" SET currency_id = (SELECT id FROM ""Currencies"" WHERE code = 'USD' LIMIT 1);
            ");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_contributions_mukando_round_id_user_id",
                table: "MukandoContributions",
                columns: new[] { "mukando_round_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mukando_contributions_user_id",
                table: "MukandoContributions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_opt_out_requests_expense_cycle_id",
                table: "MukandoOptOutRequests",
                column: "expense_cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_opt_out_requests_responded_by_user_id",
                table: "MukandoOptOutRequests",
                column: "responded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_opt_out_requests_user_id",
                table: "MukandoOptOutRequests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_payouts_confirmed_by_user_id",
                table: "MukandoPayouts",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_payouts_mukando_round_id",
                table: "MukandoPayouts",
                column: "mukando_round_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mukando_payouts_recipient_user_id",
                table: "MukandoPayouts",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_round_activities_mukando_round_id",
                table: "MukandoRoundActivities",
                column: "mukando_round_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_round_activities_user_id",
                table: "MukandoRoundActivities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_rounds_expense_cycle_id_round_number",
                table: "MukandoRounds",
                columns: new[] { "expense_cycle_id", "round_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mukando_rounds_recipient_user_id",
                table: "MukandoRounds",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_swap_requests_expense_cycle_id",
                table: "MukandoSwapRequests",
                column: "expense_cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_swap_requests_requester_round_id",
                table: "MukandoSwapRequests",
                column: "requester_round_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_swap_requests_requester_user_id",
                table: "MukandoSwapRequests",
                column: "requester_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_swap_requests_target_round_id",
                table: "MukandoSwapRequests",
                column: "target_round_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_swap_requests_target_user_id",
                table: "MukandoSwapRequests",
                column: "target_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_expense_cycles_currencies_currency_id",
                table: "ExpenseCycles",
                column: "currency_id",
                principalTable: "Currencies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_expense_cycles_currencies_currency_id",
                table: "ExpenseCycles");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "MukandoContributions");

            migrationBuilder.DropTable(
                name: "MukandoOptOutRequests");

            migrationBuilder.DropTable(
                name: "MukandoPayouts");

            migrationBuilder.DropTable(
                name: "MukandoRoundActivities");

            migrationBuilder.DropTable(
                name: "MukandoSwapRequests");

            migrationBuilder.DropTable(
                name: "MukandoRounds");

            migrationBuilder.DropIndex(
                name: "ix_expense_cycles_currency_id",
                table: "ExpenseCycles");

            migrationBuilder.DropColumn(
                name: "contribution_amount",
                table: "ExpenseCycles");

            migrationBuilder.DropColumn(
                name: "currency_id",
                table: "ExpenseCycles");

            migrationBuilder.DropColumn(
                name: "cycle_type",
                table: "ExpenseCycles");

            migrationBuilder.DropColumn(
                name: "frequency",
                table: "ExpenseCycles");
        }
    }
}
