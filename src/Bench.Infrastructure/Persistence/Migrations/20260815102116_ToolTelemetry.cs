using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ToolTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tool_telemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EmitterApp = table.Column<string>(type: "text", nullable: false),
                    EmitterPid = table.Column<int>(type: "integer", nullable: false),
                    EmitterMachine = table.Column<string>(type: "text", nullable: false),
                    ClientNameCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    ClientName = table.Column<string>(type: "text", nullable: false),
                    ClientNameReason = table.Column<string>(type: "text", nullable: false),
                    ClientVersionCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    ClientVersion = table.Column<string>(type: "text", nullable: false),
                    ClientVersionReason = table.Column<string>(type: "text", nullable: false),
                    ModelCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    ModelReason = table.Column<string>(type: "text", nullable: false),
                    Transport = table.Column<string>(type: "text", nullable: false),
                    CallerKey = table.Column<string>(type: "text", nullable: false),
                    Tool = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    ArgumentsJson = table.Column<string>(type: "text", nullable: false),
                    ArgumentsTruncatedBytes = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    ResponseChars = table.Column<int>(type: "integer", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: false),
                    ResponseTruncatedBytes = table.Column<int>(type: "integer", nullable: false),
                    TokensCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    Tokens = table.Column<long>(type: "bigint", nullable: false),
                    TokensReason = table.Column<string>(type: "text", nullable: false),
                    ServerMs = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_telemetry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tool_telemetry_At",
                table: "tool_telemetry",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_tool_telemetry_Fingerprint",
                table: "tool_telemetry",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tool_telemetry_Tool_CallerKey",
                table: "tool_telemetry",
                columns: new[] { "Tool", "CallerKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_telemetry");
        }
    }
}
