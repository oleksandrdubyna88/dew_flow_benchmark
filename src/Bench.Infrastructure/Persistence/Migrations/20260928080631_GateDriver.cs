using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GateDriver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowProductChange",
                table: "gate_runs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PredictionHash",
                table: "gate_runs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferencesHash",
                table: "gate_cells",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServerVersion",
                table: "gate_cells",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SettingsChecked",
                table: "gate_cells",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SettingsMismatches",
                table: "gate_cells",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowProductChange",
                table: "gate_runs");

            migrationBuilder.DropColumn(
                name: "PredictionHash",
                table: "gate_runs");

            migrationBuilder.DropColumn(
                name: "ReferencesHash",
                table: "gate_cells");

            migrationBuilder.DropColumn(
                name: "ServerVersion",
                table: "gate_cells");

            migrationBuilder.DropColumn(
                name: "SettingsChecked",
                table: "gate_cells");

            migrationBuilder.DropColumn(
                name: "SettingsMismatches",
                table: "gate_cells");
        }
    }
}
