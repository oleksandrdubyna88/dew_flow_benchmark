using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunTaskKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "runs",
                type: "text",
                nullable: false,
                defaultValue: "Reading");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "runs");
        }
    }
}
