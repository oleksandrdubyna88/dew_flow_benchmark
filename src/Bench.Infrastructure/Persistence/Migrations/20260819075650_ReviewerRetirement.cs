using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewerRetirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TRUE, and it is the whole migration. The scaffolder writes `false` here because that is a bool's CLR
            // default, and applying that would RETIRE the three slots this bank already has — turning a column
            // meant to unblock the panel into the thing that empties it, silently, on deploy. Existing reviewers
            // were serving before this column existed and are serving after it.
            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "reviewers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviewers_Enabled",
                table: "reviewers",
                column: "Enabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reviewers_Enabled",
                table: "reviewers");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "reviewers");
        }
    }
}
