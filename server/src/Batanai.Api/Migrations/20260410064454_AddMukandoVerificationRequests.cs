using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMukandoVerificationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MukandoVerificationRequests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mukando_round_id = table.Column<int>(type: "integer", nullable: false),
                    target = table.Column<int>(type: "integer", nullable: false),
                    mukando_contribution_id = table.Column<int>(type: "integer", nullable: true),
                    pending_payout_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    pending_payout_method = table.Column<int>(type: "integer", nullable: true),
                    pending_payout_proof_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pending_payout_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    initiated_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    assigned_to_user_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    responded_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    responded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mukando_verification_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_mukando_verification_requests_mukando_rounds_mukando_round_id",
                        column: x => x.mukando_round_id,
                        principalTable: "MukandoRounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mukando_verification_requests_users_assigned_to_user_id",
                        column: x => x.assigned_to_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mukando_verification_requests_users_initiated_by_user_id",
                        column: x => x.initiated_by_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mukando_verification_requests_assigned_to_user_id",
                table: "MukandoVerificationRequests",
                column: "assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_verification_requests_initiated_by_user_id",
                table: "MukandoVerificationRequests",
                column: "initiated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mukando_verification_requests_mukando_round_id",
                table: "MukandoVerificationRequests",
                column: "mukando_round_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MukandoVerificationRequests");
        }
    }
}
