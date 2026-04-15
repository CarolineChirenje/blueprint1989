using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSentEmailsAndBccConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sent_emails",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    to_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: false),
                    plain_body = table.Column<string>(type: "text", nullable: true),
                    bcc_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sent_emails", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sent_emails_sent_at",
                table: "sent_emails",
                column: "sent_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sent_emails");
        }
    }
}
