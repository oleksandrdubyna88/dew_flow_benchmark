using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gate_reviewers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Runtime = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    EndpointUrl = table.Column<string>(type: "text", nullable: false),
                    EndpointRef = table.Column<string>(type: "text", nullable: false),
                    KeyName = table.Column<string>(type: "text", nullable: false),
                    CredsKeyRef = table.Column<string>(type: "text", nullable: false),
                    ExecutableRef = table.Column<string>(type: "text", nullable: false),
                    RemoteVendor = table.Column<string>(type: "text", nullable: false),
                    Dialect = table.Column<string>(type: "text", nullable: false),
                    ReasoningEffort = table.Column<string>(type: "text", nullable: false),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    TimeoutMinutes = table.Column<int>(type: "integer", nullable: false),
                    FollowUps = table.Column<int>(type: "integer", nullable: false),
                    ReviewMinutesCap = table.Column<int>(type: "integer", nullable: false),
                    Thinking = table.Column<bool>(type: "boolean", nullable: false),
                    PricesKnown = table.Column<bool>(type: "boolean", nullable: false),
                    InPerMTok = table.Column<decimal>(type: "numeric", nullable: false),
                    CachedPerMTok = table.Column<decimal>(type: "numeric", nullable: false),
                    OutPerMTok = table.Column<decimal>(type: "numeric", nullable: false),
                    TierFromTokens = table.Column<long>(type: "bigint", nullable: false),
                    TierIn = table.Column<decimal>(type: "numeric", nullable: false),
                    TierCached = table.Column<decimal>(type: "numeric", nullable: false),
                    TierOut = table.Column<decimal>(type: "numeric", nullable: false),
                    GatePlan = table.Column<bool>(type: "boolean", nullable: false),
                    GateCode = table.Column<bool>(type: "boolean", nullable: false),
                    GateFeature = table.Column<bool>(type: "boolean", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_reviewers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gate_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Gate = table.Column<string>(type: "text", nullable: false),
                    SuiteStamp = table.Column<string>(type: "text", nullable: false),
                    DataDirMode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gate_artifacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CellId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Class = table.Column<string>(type: "text", nullable: false),
                    RelativePath = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gate_artifacts_gate_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "gate_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gate_cells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "text", nullable: false),
                    ReviewerId = table.Column<string>(type: "text", nullable: false),
                    Repeat = table.Column<int>(type: "integer", nullable: false),
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
                    OutcomeKind = table.Column<string>(type: "text", nullable: false),
                    FactsRecorded = table.Column<bool>(type: "boolean", nullable: false),
                    Valid = table.Column<bool>(type: "boolean", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: false),
                    ReplyParsed = table.Column<bool>(type: "boolean", nullable: false),
                    FindingsCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    Findings = table.Column<long>(type: "bigint", nullable: false),
                    Turns = table.Column<int>(type: "integer", nullable: false),
                    HttpCalls = table.Column<int>(type: "integer", nullable: false),
                    FinishReasons = table.Column<List<string>>(type: "text[]", nullable: false),
                    Statuses = table.Column<List<int>>(type: "integer[]", nullable: false),
                    TokensInCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    TokensIn = table.Column<long>(type: "bigint", nullable: false),
                    TokensOutCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    TokensOut = table.Column<long>(type: "bigint", nullable: false),
                    TokensCachedCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    TokensCached = table.Column<long>(type: "bigint", nullable: false),
                    TokensReasoningCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    TokensReasoning = table.Column<long>(type: "bigint", nullable: false),
                    SecondsTotal = table.Column<double>(type: "double precision", nullable: false),
                    ReviewSeconds = table.Column<double>(type: "double precision", nullable: false),
                    SecondsPerTurn = table.Column<List<double>>(type: "double precision[]", nullable: false),
                    CachedPerCallCaptured = table.Column<List<bool>>(type: "boolean[]", nullable: false),
                    CachedPerCall = table.Column<List<long>>(type: "bigint[]", nullable: false),
                    CostCaptured = table.Column<bool>(type: "boolean", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    Served = table.Column<int>(type: "integer", nullable: false),
                    Refused = table.Column<int>(type: "integer", nullable: false),
                    FailureKind = table.Column<string>(type: "text", nullable: false),
                    FailureText = table.Column<string>(type: "text", nullable: false),
                    SettingsHash = table.Column<string>(type: "text", nullable: false),
                    PromptHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_cells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gate_cells_gate_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "gate_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gate_findings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CellId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsGating = table.Column<bool>(type: "boolean", nullable: false),
                    Line = table.Column<int>(type: "integer", nullable: false),
                    TextHash = table.Column<string>(type: "text", nullable: false),
                    FileHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gate_findings_gate_cells_CellId",
                        column: x => x.CellId,
                        principalTable: "gate_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gate_verdicts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CellId = table.Column<Guid>(type: "uuid", nullable: false),
                    FindingOrdinal = table.Column<int>(type: "integer", nullable: false),
                    RubricId = table.Column<string>(type: "text", nullable: false),
                    RubricKind = table.Column<string>(type: "text", nullable: false),
                    RubricHash = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Reading = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    SeverityFair = table.Column<string>(type: "text", nullable: false),
                    Grounded = table.Column<string>(type: "text", nullable: false),
                    ClusterHash = table.Column<string>(type: "text", nullable: false),
                    SeedHit = table.Column<string>(type: "text", nullable: false),
                    WorthHaving = table.Column<bool>(type: "boolean", nullable: false),
                    FailureCause = table.Column<string>(type: "text", nullable: false),
                    AssessorId = table.Column<string>(type: "text", nullable: false),
                    BatchId = table.Column<string>(type: "text", nullable: false),
                    PromptHash = table.Column<string>(type: "text", nullable: false),
                    AssessorFamilyMatches = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_verdicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gate_verdicts_gate_cells_CellId",
                        column: x => x.CellId,
                        principalTable: "gate_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gate_artifacts_RelativePath",
                table: "gate_artifacts",
                column: "RelativePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gate_artifacts_RunId_CellId_Attempt",
                table: "gate_artifacts",
                columns: new[] { "RunId", "CellId", "Attempt" });

            migrationBuilder.CreateIndex(
                name: "IX_gate_cells_RunId_State_Position",
                table: "gate_cells",
                columns: new[] { "RunId", "State", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_gate_cells_State_ClaimedAt",
                table: "gate_cells",
                columns: new[] { "State", "ClaimedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_gate_findings_CellId_Attempt_Ordinal",
                table: "gate_findings",
                columns: new[] { "CellId", "Attempt", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gate_reviewers_Hash",
                table: "gate_reviewers",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_gate_runs_CreatedAt",
                table: "gate_runs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_gate_verdicts_CellId_FindingOrdinal_RubricHash",
                table: "gate_verdicts",
                columns: new[] { "CellId", "FindingOrdinal", "RubricHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gate_artifacts");

            migrationBuilder.DropTable(
                name: "gate_findings");

            migrationBuilder.DropTable(
                name: "gate_reviewers");

            migrationBuilder.DropTable(
                name: "gate_verdicts");

            migrationBuilder.DropTable(
                name: "gate_cells");

            migrationBuilder.DropTable(
                name: "gate_runs");
        }
    }
}
