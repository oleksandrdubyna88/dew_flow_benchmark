using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GateThinkingThreeStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "Thinking",
                table: "gate_reviewers",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }

        /// <summary>Deliberately strict: a database holding a vendor-default row (NULL) FAILS this rollback rather than
        /// relabelling that row as off — mapping NULL to false would change the row's definition, its hash would stop
        /// matching, and the catalog would refuse it as edited in place. Rolling back is only possible while no row asks
        /// for the vendor's default (the code round, 2026-09-29, asked for this to be said).</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "Thinking",
                table: "gate_reviewers",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);
        }
    }
}
