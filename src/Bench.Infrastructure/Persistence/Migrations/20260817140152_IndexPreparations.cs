using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndexPreparations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "index_preparations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitSha = table.Column<string>(type: "text", nullable: false),
                    RecipeHash = table.Column<string>(type: "text", nullable: false),
                    EngineEndpoint = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    OwnerHost = table.Column<string>(type: "text", nullable: false),
                    OwnerPid = table.Column<int>(type: "integer", nullable: false),
                    PassId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Heartbeat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_index_preparations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_index_preparations_CommitSha_RecipeHash_EngineEndpoint",
                table: "index_preparations",
                columns: new[] { "CommitSha", "RecipeHash", "EngineEndpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_index_preparations_State_Heartbeat",
                table: "index_preparations",
                columns: new[] { "State", "Heartbeat" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "index_preparations");
        }
    }
}
