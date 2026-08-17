using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetrievalEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "{}", not "" — the generated default was the CLR default for a string, and '' is not valid
            // jsonb, so this ALTER TABLE would have failed on any database that already held a result. An
            // empty object is also the honest value for a row written before these columns existed:
            // ResponseMetaJson.Read turns it into ResponseMeta.None, which says nobody counted rather than
            // claiming a leg that reported zeroes.
            migrationBuilder.AddColumn<string>(
                name: "ResponseMetaJson",
                table: "results",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "ThinkingReason",
                table: "results",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ThinkingText",
                table: "results",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "funnels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<string>(type: "text", nullable: false),
                    StagesJson = table.Column<string>(type: "jsonb", nullable: false),
                    TotalMs = table.Column<long>(type: "bigint", nullable: false),
                    AbsentJson = table.Column<string>(type: "jsonb", nullable: false),
                    Degraded = table.Column<bool>(type: "boolean", nullable: false),
                    DegradationReason = table.Column<string>(type: "text", nullable: false),
                    PayloadBytes = table.Column<long>(type: "bigint", nullable: false),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    Collection = table.Column<string>(type: "text", nullable: false),
                    RequestedAxesJson = table.Column<string>(type: "jsonb", nullable: false),
                    AppliedAxesJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funnels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_funnels_results_ResultId",
                        column: x => x.ResultId,
                        principalTable: "results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "retrieved_hits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    RelativePath = table.Column<string>(type: "text", nullable: false),
                    StartLine = table.Column<int>(type: "integer", nullable: false),
                    EndLine = table.Column<int>(type: "integer", nullable: false),
                    Member = table.Column<string>(type: "text", nullable: false),
                    MemberKey = table.Column<string>(type: "text", nullable: false),
                    Signature = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    Ordering = table.Column<string>(type: "text", nullable: false),
                    ChannelsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RanksJson = table.Column<string>(type: "jsonb", nullable: false),
                    Snippet = table.Column<string>(type: "text", nullable: false),
                    SnippetBytes = table.Column<long>(type: "bigint", nullable: false),
                    SnippetPrunedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retrieved_hits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_retrieved_hits_results_ResultId",
                        column: x => x.ResultId,
                        principalTable: "results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_funnels_Degraded",
                table: "funnels",
                column: "Degraded");

            migrationBuilder.CreateIndex(
                name: "IX_funnels_ResultId",
                table: "funnels",
                column: "ResultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_retrieved_hits_CreatedAt_SnippetPrunedAt",
                table: "retrieved_hits",
                columns: new[] { "CreatedAt", "SnippetPrunedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_retrieved_hits_RelativePath",
                table: "retrieved_hits",
                column: "RelativePath");

            migrationBuilder.CreateIndex(
                name: "IX_retrieved_hits_ResultId_Rank",
                table: "retrieved_hits",
                columns: new[] { "ResultId", "Rank" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funnels");

            migrationBuilder.DropTable(
                name: "retrieved_hits");

            migrationBuilder.DropColumn(
                name: "ResponseMetaJson",
                table: "results");

            migrationBuilder.DropColumn(
                name: "ThinkingReason",
                table: "results");

            migrationBuilder.DropColumn(
                name: "ThinkingText",
                table: "results");
        }
    }
}
