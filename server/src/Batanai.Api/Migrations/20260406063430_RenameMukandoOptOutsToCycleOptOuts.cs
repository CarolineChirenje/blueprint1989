using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameMukandoOptOutsToCycleOptOuts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "MukandoOptOutRequests",
                newName: "CycleOptOutRequests");

            migrationBuilder.RenameIndex(
                name: "ix_mukando_opt_out_requests_expense_cycle_id",
                table: "CycleOptOutRequests",
                newName: "ix_cycle_opt_out_requests_expense_cycle_id");

            migrationBuilder.RenameIndex(
                name: "ix_mukando_opt_out_requests_responded_by_user_id",
                table: "CycleOptOutRequests",
                newName: "ix_cycle_opt_out_requests_responded_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_mukando_opt_out_requests_user_id",
                table: "CycleOptOutRequests",
                newName: "ix_cycle_opt_out_requests_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "CycleOptOutRequests",
                newName: "MukandoOptOutRequests");

            migrationBuilder.RenameIndex(
                name: "ix_cycle_opt_out_requests_expense_cycle_id",
                table: "MukandoOptOutRequests",
                newName: "ix_mukando_opt_out_requests_expense_cycle_id");

            migrationBuilder.RenameIndex(
                name: "ix_cycle_opt_out_requests_responded_by_user_id",
                table: "MukandoOptOutRequests",
                newName: "ix_mukando_opt_out_requests_responded_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_cycle_opt_out_requests_user_id",
                table: "MukandoOptOutRequests",
                newName: "ix_mukando_opt_out_requests_user_id");
        }
    }
}
