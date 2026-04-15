using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddImageToFeatureBugReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "image_file_id",
                table: "FeatureBugReports",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_feature_bug_reports_image_file_id",
                table: "FeatureBugReports",
                column: "image_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_feature_bug_reports_uploaded_files_image_file_id",
                table: "FeatureBugReports",
                column: "image_file_id",
                principalTable: "uploaded_files",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_feature_bug_reports_uploaded_files_image_file_id",
                table: "FeatureBugReports");

            migrationBuilder.DropIndex(
                name: "ix_feature_bug_reports_image_file_id",
                table: "FeatureBugReports");

            migrationBuilder.DropColumn(
                name: "image_file_id",
                table: "FeatureBugReports");
        }
    }
}
