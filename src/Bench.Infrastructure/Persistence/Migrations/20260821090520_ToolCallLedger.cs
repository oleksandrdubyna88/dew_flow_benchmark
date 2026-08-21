using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ToolCallLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ToolsOffered",
                table: "results",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "tool_calls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Turn = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    ToolName = table.Column<string>(type: "text", nullable: false),
                    ArgumentsJson = table.Column<string>(type: "text", nullable: false),
                    Refused = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_calls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tool_calls_results_ResultId",
                        column: x => x.ResultId,
                        principalTable: "results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tool_calls_ResultId_Ordinal",
                table: "tool_calls",
                columns: new[] { "ResultId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tool_calls_ToolName",
                table: "tool_calls",
                column: "ToolName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_calls");

            migrationBuilder.DropColumn(
                name: "ToolsOffered",
                table: "results");
        }
    }
}
