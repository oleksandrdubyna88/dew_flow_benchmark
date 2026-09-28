using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GateAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gate_hand_checks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Campaigns = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    RubricId = table.Column<string>(type: "text", nullable: false),
                    RubricKind = table.Column<string>(type: "text", nullable: false),
                    RubricHash = table.Column<string>(type: "text", nullable: false),
                    AssessorId = table.Column<string>(type: "text", nullable: false),
                    Read = table.Column<int>(type: "integer", nullable: false),
                    Agreed = table.Column<int>(type: "integer", nullable: false),
                    NoteHash = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_hand_checks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gate_verdicts_CellId_FindingOrdinal_RubricHash_AssessorId_B~",
                table: "gate_verdicts",
                columns: new[] { "CellId", "FindingOrdinal", "RubricHash", "AssessorId", "BatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gate_hand_checks_RubricHash_AssessorId",
                table: "gate_hand_checks",
                columns: new[] { "RubricHash", "AssessorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gate_hand_checks");

            migrationBuilder.DropIndex(
                name: "IX_gate_verdicts_CellId_FindingOrdinal_RubricHash_AssessorId_B~",
                table: "gate_verdicts");
        }
    }
}
