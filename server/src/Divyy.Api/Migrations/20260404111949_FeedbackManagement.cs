using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Divvy.Api.Migrations
{
    /// <inheritdoc />
    public partial class FeedbackManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureBugReports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    version_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    submitted_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_bug_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_feature_bug_reports_users_submitted_by_user_id",
                        column: x => x.submitted_by_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeatureBugReportCategories",
                columns: table => new
                {
                    feature_bug_report_id = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_bug_report_categories", x => new { x.feature_bug_report_id, x.category });
                    table.ForeignKey(
                        name: "fk_feature_bug_report_categories_feature_bug_reports_feature_bug_re",
                        column: x => x.feature_bug_report_id,
                        principalTable: "FeatureBugReports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_feature_bug_reports_submitted_by_user_id",
                table: "FeatureBugReports",
                column: "submitted_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureBugReportCategories");

            migrationBuilder.DropTable(
                name: "FeatureBugReports");
        }
    }
}
