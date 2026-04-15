using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReminderJobs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    notification_type = table.Column<int>(type: "integer", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_id = table.Column<int>(type: "integer", nullable: false),
                    stage_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_for_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    deep_link_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    related_entity_id = table.Column<int>(type: "integer", nullable: true),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminder_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_reminder_jobs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reminder_jobs_idempotency_key",
                table: "ReminderJobs",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reminder_jobs_user_id",
                table: "ReminderJobs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderJobs_Status_ScheduledForUtc",
                table: "ReminderJobs",
                columns: new[] { "status", "scheduled_for_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReminderJobs_Subject_Status",
                table: "ReminderJobs",
                columns: new[] { "subject_type", "subject_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReminderJobs");
        }
    }
}
