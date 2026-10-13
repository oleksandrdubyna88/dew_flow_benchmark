using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScoutTaskPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroundTruthCommitment",
                table: "scout_tasks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "scout_tasks",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Every task frozen before this migration was public (E1 held only public tasks); an empty name would not read back as an enum.
            migrationBuilder.AddColumn<string>(
                name: "Privacy",
                table: "scout_tasks",
                type: "text",
                nullable: false,
                defaultValue: "Public");

            migrationBuilder.AddColumn<string>(
                name: "StatementCommitment",
                table: "scout_tasks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Campaign",
                table: "scout_runs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroundTruthCommitment",
                table: "scout_tasks");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "scout_tasks");

            migrationBuilder.DropColumn(
                name: "Privacy",
                table: "scout_tasks");

            migrationBuilder.DropColumn(
                name: "StatementCommitment",
                table: "scout_tasks");

            migrationBuilder.DropColumn(
                name: "Campaign",
                table: "scout_runs");
        }
    }
}
