using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    RepoUrl = table.Column<string>(type: "text", nullable: false),
                    CommitSha = table.Column<string>(type: "text", nullable: false),
                    Exclusions = table.Column<List<string>>(type: "text[]", nullable: false),
                    EngineKind = table.Column<string>(type: "text", nullable: false),
                    EngineEndpoint = table.Column<string>(type: "text", nullable: false),
                    EngineVersion = table.Column<string>(type: "text", nullable: false),
                    IndexFingerprint = table.Column<string>(type: "text", nullable: false),
                    SuiteStamp = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<string>(type: "text", nullable: false),
                    Repeat = table.Column<int>(type: "integer", nullable: false),
                    Leg = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OutcomeKind = table.Column<string>(type: "text", nullable: false),
                    OutcomeDetail = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cells_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cells_RunId_State_Position",
                table: "cells",
                columns: new[] { "RunId", "State", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_cells_State_ClaimedAt",
                table: "cells",
                columns: new[] { "State", "ClaimedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cells");

            migrationBuilder.DropTable(
                name: "runs");
        }
    }
}
