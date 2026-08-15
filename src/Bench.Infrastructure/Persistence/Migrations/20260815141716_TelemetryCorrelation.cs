using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TelemetryCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Leg",
                table: "tool_telemetry",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "LegCaptured",
                table: "tool_telemetry",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegReason",
                table: "tool_telemetry",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "tool_telemetry",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "PhaseCaptured",
                table: "tool_telemetry",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PhaseReason",
                table: "tool_telemetry",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_tool_telemetry_Leg_Phase",
                table: "tool_telemetry",
                columns: new[] { "Leg", "Phase" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tool_telemetry_Leg_Phase",
                table: "tool_telemetry");

            migrationBuilder.DropColumn(
                name: "Leg",
                table: "tool_telemetry");

            migrationBuilder.DropColumn(
                name: "LegCaptured",
                table: "tool_telemetry");

            migrationBuilder.DropColumn(
                name: "LegReason",
                table: "tool_telemetry");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "tool_telemetry");

            migrationBuilder.DropColumn(
                name: "PhaseCaptured",
                table: "tool_telemetry");

            migrationBuilder.DropColumn(
                name: "PhaseReason",
                table: "tool_telemetry");
        }
    }
}
