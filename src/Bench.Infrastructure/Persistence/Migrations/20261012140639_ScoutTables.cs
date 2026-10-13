using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScoutTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scout_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    Repeats = table.Column<int>(type: "integer", nullable: false),
                    PinCliVersion = table.Column<string>(type: "text", nullable: false),
                    PinPluginCacheHash = table.Column<string>(type: "text", nullable: false),
                    PinResolvedOpus = table.Column<string>(type: "text", nullable: false),
                    PinResolvedSonnet = table.Column<string>(type: "text", nullable: false),
                    PinSettingsHash = table.Column<string>(type: "text", nullable: false),
                    Arms = table.Column<List<string>>(type: "text[]", nullable: false),
                    TaskIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    ArmCapsUsd = table.Column<List<decimal>>(type: "numeric[]", nullable: false),
                    ArmWallSeconds = table.Column<List<int>>(type: "integer[]", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    CeilingUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReservedUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    SettledUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnknownUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scout_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scout_cells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "text", nullable: false),
                    Arm = table.Column<string>(type: "text", nullable: false),
                    Repeat = table.Column<int>(type: "integer", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
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
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RecallFoundCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    RecallFound = table.Column<long>(type: "bigint", nullable: false),
                    RecallTotalCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    RecallTotal = table.Column<long>(type: "bigint", nullable: false),
                    TeamMsCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    TeamMs = table.Column<long>(type: "bigint", nullable: false),
                    SessionMsCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    SessionMs = table.Column<long>(type: "bigint", nullable: false),
                    ComputedUsdCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    ComputedUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReportedUsdCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    ReportedUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    TeamTokensCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    TeamInputTokens = table.Column<long>(type: "bigint", nullable: false),
                    TeamOutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    TeamCacheReadTokens = table.Column<long>(type: "bigint", nullable: false),
                    TeamCacheWrite5mTokens = table.Column<long>(type: "bigint", nullable: false),
                    TeamCacheWrite1hTokens = table.Column<long>(type: "bigint", nullable: false),
                    TeamUsdCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    TeamUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scout_cells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scout_cells_scout_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "scout_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scout_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "text", nullable: false),
                    Repository = table.Column<string>(type: "text", nullable: false),
                    ParentSha = table.Column<string>(type: "text", nullable: false),
                    FixSha = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Statement = table.Column<string>(type: "text", nullable: false),
                    GroundTruth = table.Column<List<string>>(type: "text[]", nullable: false),
                    Removals = table.Column<List<string>>(type: "text[]", nullable: false),
                    TreeHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scout_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scout_tasks_scout_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "scout_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scout_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CellId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    ReservedUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    SettledUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scout_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scout_attempts_scout_cells_CellId",
                        column: x => x.CellId,
                        principalTable: "scout_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scout_attempts_scout_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "scout_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scout_claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CellId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    LineCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    Line = table.Column<long>(type: "bigint", nullable: false),
                    StatementSha256 = table.Column<string>(type: "text", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: false),
                    AssessorModel = table.Column<string>(type: "text", nullable: false),
                    RubricSha256 = table.Column<string>(type: "text", nullable: false),
                    AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scout_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scout_claims_scout_cells_CellId",
                        column: x => x.CellId,
                        principalTable: "scout_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scout_claims_scout_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "scout_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scout_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CellId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Focus = table.Column<string>(type: "text", nullable: false),
                    InputModel = table.Column<string>(type: "text", nullable: false),
                    PromptSha256 = table.Column<string>(type: "text", nullable: false),
                    ResolvedModel = table.Column<string>(type: "text", nullable: false),
                    DurationCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheReadTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheWrite5mTokens = table.Column<long>(type: "bigint", nullable: false),
                    CacheWrite1hTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputSeparable = table.Column<bool>(type: "boolean", nullable: false),
                    CostCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    KeyFiles = table.Column<List<string>>(type: "text[]", nullable: false),
                    KeyFilesUnparseable = table.Column<int>(type: "integer", nullable: false),
                    KeyFilesExtra = table.Column<int>(type: "integer", nullable: false),
                    Silent = table.Column<bool>(type: "boolean", nullable: false),
                    ClaimsParsed = table.Column<int>(type: "integer", nullable: false),
                    ClaimsUnparseable = table.Column<int>(type: "integer", nullable: false),
                    ClaimsExtra = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scout_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scout_records_scout_cells_CellId",
                        column: x => x.CellId,
                        principalTable: "scout_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scout_attempts_CellId_Number",
                table: "scout_attempts",
                columns: new[] { "CellId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scout_attempts_RunId_State",
                table: "scout_attempts",
                columns: new[] { "RunId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_scout_cells_RunId_State_Slot_Position",
                table: "scout_cells",
                columns: new[] { "RunId", "State", "Slot", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_scout_cells_RunId_TaskId_Arm_Repeat_Generation",
                table: "scout_cells",
                columns: new[] { "RunId", "TaskId", "Arm", "Repeat", "Generation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scout_cells_State_ClaimedAt",
                table: "scout_cells",
                columns: new[] { "State", "ClaimedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_scout_claims_CellId_Ordinal_Index",
                table: "scout_claims",
                columns: new[] { "CellId", "Ordinal", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scout_claims_RunId",
                table: "scout_claims",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_scout_records_CellId_Ordinal",
                table: "scout_records",
                columns: new[] { "CellId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scout_runs_CreatedAt",
                table: "scout_runs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_scout_tasks_RunId_TaskId",
                table: "scout_tasks",
                columns: new[] { "RunId", "TaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scout_attempts");

            migrationBuilder.DropTable(
                name: "scout_claims");

            migrationBuilder.DropTable(
                name: "scout_records");

            migrationBuilder.DropTable(
                name: "scout_tasks");

            migrationBuilder.DropTable(
                name: "scout_cells");

            migrationBuilder.DropTable(
                name: "scout_runs");
        }
    }
}
