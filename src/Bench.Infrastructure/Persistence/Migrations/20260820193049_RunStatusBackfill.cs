using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <summary>Every run stored before the status lifecycle existed reads <c>Planned</c> — including
    /// sixteen finished campaigns, which tells an operator the opposite of the truth. A run whose every
    /// cell is terminal is retro-marked <c>Completed</c>; one with work still open, or with no cells at
    /// all, is left exactly as it stands, because "resumable" and "never started" are its honest states.
    /// Data only — no schema change, and no Down: un-completing history would be inventing it.</summary>
    public partial class RunStatusBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE runs SET "Status" = 'Completed'
                WHERE "Status" = 'Planned'
                  AND EXISTS (SELECT 1 FROM cells c WHERE c."RunId" = runs."Id")
                  AND NOT EXISTS (
                      SELECT 1 FROM cells c
                      WHERE c."RunId" = runs."Id" AND c."State" IN ('Pending', 'Claimed'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty: rolling history back to "Planned" would be inventing it.
        }
    }
}
