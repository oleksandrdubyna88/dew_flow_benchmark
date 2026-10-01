using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProbeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "probe_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OracleVersion = table.Column<string>(type: "text", nullable: false),
                    OracleSource = table.Column<string>(type: "text", nullable: false),
                    Repeats = table.Column<int>(type: "integer", nullable: false),
                    ArtifactsPruned = table.Column<bool>(type: "boolean", nullable: false),
                    SubjectIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    SubjectRuntimes = table.Column<List<string>>(type: "text[]", nullable: false),
                    SubjectModels = table.Column<List<string>>(type: "text[]", nullable: false),
                    SubjectExecutableRefs = table.Column<List<string>>(type: "text[]", nullable: false),
                    SubjectVendors = table.Column<List<string>>(type: "text[]", nullable: false),
                    SubjectEndpoints = table.Column<List<string>>(type: "text[]", nullable: false),
                    SubjectDialects = table.Column<List<string>>(type: "text[]", nullable: false),
                    SubjectConfinements = table.Column<List<string>>(type: "text[]", nullable: false),
                    Probes = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_probe_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "probe_cells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Probe = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<string>(type: "text", nullable: false),
                    Repeat = table.Column<int>(type: "integer", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    UnmeasuredAttempts = table.Column<int>(type: "integer", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    OwnerHost = table.Column<string>(type: "text", nullable: false),
                    OwnerPid = table.Column<int>(type: "integer", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PinBinarySha256 = table.Column<string>(type: "text", nullable: false),
                    PinVersionText = table.Column<string>(type: "text", nullable: false),
                    PinGitSha = table.Column<string>(type: "text", nullable: false),
                    PinDirtyCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    PinDirtyFiles = table.Column<long>(type: "bigint", nullable: false),
                    PinCheckedTree = table.Column<string>(type: "text", nullable: false),
                    AttemptKind = table.Column<string>(type: "text", nullable: false),
                    ExitCodeCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    ExitCode = table.Column<long>(type: "bigint", nullable: false),
                    CanaryRead = table.Column<string>(type: "text", nullable: false),
                    ReadAttempted = table.Column<string>(type: "text", nullable: false),
                    AnswerCurrent = table.Column<string>(type: "text", nullable: false),
                    ToolEvidence = table.Column<string>(type: "text", nullable: false),
                    ShellUsed = table.Column<string>(type: "text", nullable: false),
                    ReaderOffered = table.Column<string>(type: "text", nullable: false),
                    Reachable = table.Column<string>(type: "text", nullable: false),
                    AccountOut = table.Column<string>(type: "text", nullable: false),
                    ArtifactKinds = table.Column<List<string>>(type: "text[]", nullable: false),
                    ArtifactPaths = table.Column<List<string>>(type: "text[]", nullable: false),
                    ArtifactSha256s = table.Column<List<string>>(type: "text[]", nullable: false),
                    ArtifactLengths = table.Column<List<long>>(type: "bigint[]", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_probe_cells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_probe_cells_probe_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "probe_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_probe_cells_RunId_Probe_SubjectId_Repeat_Generation",
                table: "probe_cells",
                columns: new[] { "RunId", "Probe", "SubjectId", "Repeat", "Generation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_probe_cells_RunId_SubjectId_State_Slot_Position",
                table: "probe_cells",
                columns: new[] { "RunId", "SubjectId", "State", "Slot", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_probe_cells_State_ClaimedAt",
                table: "probe_cells",
                columns: new[] { "State", "ClaimedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_probe_runs_CreatedAt",
                table: "probe_runs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "probe_cells");

            migrationBuilder.DropTable(
                name: "probe_runs");
        }
    }
}
