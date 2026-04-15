using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Batanai.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTourCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "tour_completed_at",
                table: "Users",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tour_completed_at",
                table: "Users");
        }
    }
}
