using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GateImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TurnFactsCaptured",
                table: "gate_cells",
                type: "boolean",
                nullable: false,

                // Every row before E5 is a native session, and a native session recorded its turns.
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "gate_summaries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Gate = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Document = table.Column<string>(type: "text", nullable: false),
                    Section = table.Column<string>(type: "text", nullable: false),
                    DocumentSha256 = table.Column<string>(type: "text", nullable: false),
                    RowOrdinal = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Metric = table.Column<string>(type: "text", nullable: false),
                    Captured = table.Column<bool>(type: "boolean", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_summaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gate_summaries_DocumentSha256_Section_RowOrdinal_Metric",
                table: "gate_summaries",
                columns: new[] { "DocumentSha256", "Section", "RowOrdinal", "Metric" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gate_summaries");

            migrationBuilder.DropColumn(
                name: "TurnFactsCaptured",
                table: "gate_cells");
        }
    }
}
