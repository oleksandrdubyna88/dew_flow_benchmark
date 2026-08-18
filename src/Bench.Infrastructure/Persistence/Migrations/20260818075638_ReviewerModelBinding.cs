using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewerModelBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModelKey",
                table: "reviewers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelKey",
                table: "reviewers");
        }
    }
}
