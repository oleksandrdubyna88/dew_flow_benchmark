using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionTraces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    SessionKey = table.Column<string>(type: "text", nullable: false),
                    Runtime = table.Column<string>(type: "text", nullable: false),
                    TaskId = table.Column<string>(type: "text", nullable: false),
                    TaskName = table.Column<string>(type: "text", nullable: false),
                    PlanPath = table.Column<string>(type: "text", nullable: false),
                    WorkspacePath = table.Column<string>(type: "text", nullable: false),
                    Branch = table.Column<string>(type: "text", nullable: false),
                    ModelCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    ModelReason = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastEventAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "session_tool_calls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    OpenFingerprint = table.Column<string>(type: "text", nullable: false),
                    CloseFingerprint = table.Column<string>(type: "text", nullable: false),
                    ToolName = table.Column<string>(type: "text", nullable: false),
                    Class = table.Column<string>(type: "text", nullable: false),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    Target = table.Column<string>(type: "text", nullable: false),
                    ArgumentsJson = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    ResponseCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    Response = table.Column<string>(type: "text", nullable: false),
                    ResponseReason = table.Column<string>(type: "text", nullable: false),
                    ResponseChars = table.Column<int>(type: "integer", nullable: false),
                    DurationCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    DurationReason = table.Column<string>(type: "text", nullable: false),
                    Mutation = table.Column<string>(type: "text", nullable: false),
                    DigestBefore = table.Column<string>(type: "text", nullable: false),
                    DigestAfter = table.Column<string>(type: "text", nullable: false),
                    DirtyBefore = table.Column<int>(type: "integer", nullable: false),
                    DirtyAfter = table.Column<int>(type: "integer", nullable: false),
                    CompileFailure = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tool_calls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_tool_calls_session_runs_SessionId",
                        column: x => x.SessionId,
                        principalTable: "session_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_runs_LastEventAt",
                table: "session_runs",
                column: "LastEventAt");

            migrationBuilder.CreateIndex(
                name: "IX_session_runs_PlanPath",
                table: "session_runs",
                column: "PlanPath");

            migrationBuilder.CreateIndex(
                name: "IX_session_runs_Source_SessionKey",
                table: "session_runs",
                columns: new[] { "Source", "SessionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_tool_calls_CloseFingerprint",
                table: "session_tool_calls",
                column: "CloseFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_session_tool_calls_OpenFingerprint",
                table: "session_tool_calls",
                column: "OpenFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_tool_calls_SessionId_Ordinal",
                table: "session_tool_calls",
                columns: new[] { "SessionId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_tool_calls_SessionId_State_ToolName",
                table: "session_tool_calls",
                columns: new[] { "SessionId", "State", "ToolName" });

            migrationBuilder.CreateIndex(
                name: "IX_session_tool_calls_ToolName",
                table: "session_tool_calls",
                column: "ToolName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_tool_calls");

            migrationBuilder.DropTable(
                name: "session_runs");
        }
    }
}
