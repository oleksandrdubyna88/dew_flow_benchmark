using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LegPhases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leg_phases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CellId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Tools = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Thinking = table.Column<TimeSpan>(type: "interval", nullable: false),
                    InfrastructureWait = table.Column<TimeSpan>(type: "interval", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    OutcomeKind = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leg_phases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leg_phases_cells_CellId",
                        column: x => x.CellId,
                        principalTable: "cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leg_phases_CellId_Ordinal",
                table: "leg_phases",
                columns: new[] { "CellId", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leg_phases");
        }
    }
}
