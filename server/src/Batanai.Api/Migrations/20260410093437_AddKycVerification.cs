using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKycVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "kyc_status",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserKycDocuments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    id_type = table.Column<int>(type: "integer", nullable: false),
                    id_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    full_name_on_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    document_file_id = table.Column<int>(type: "integer", nullable: true),
                    selfie_with_id_file_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    reviewed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    admin_bypass_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_kyc_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_kyc_documents_uploaded_files_document_file_id",
                        column: x => x.document_file_id,
                        principalTable: "uploaded_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_kyc_documents_uploaded_files_selfie_with_id_file_id",
                        column: x => x.selfie_with_id_file_id,
                        principalTable: "uploaded_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_kyc_documents_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_kyc_documents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_kyc_documents_document_file_id",
                table: "UserKycDocuments",
                column: "document_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_kyc_documents_reviewed_by_user_id",
                table: "UserKycDocuments",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_kyc_documents_selfie_with_id_file_id",
                table: "UserKycDocuments",
                column: "selfie_with_id_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserKycDocuments_Status",
                table: "UserKycDocuments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_UserKycDocuments_UserId",
                table: "UserKycDocuments",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserKycDocuments");

            migrationBuilder.DropColumn(
                name: "kyc_status",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "Users");
        }
    }
}
