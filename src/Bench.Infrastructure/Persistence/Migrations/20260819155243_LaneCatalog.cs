using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LaneCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lanes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    ToolsHash = table.Column<string>(type: "text", nullable: false),
                    DescriptionSet = table.Column<string>(type: "text", nullable: false),
                    DoctrineHash = table.Column<string>(type: "text", nullable: false),
                    Presentation = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lanes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lanes_Hash",
                table: "lanes",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_lanes_Name",
                table: "lanes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lanes_ToolsHash_Presentation",
                table: "lanes",
                columns: new[] { "ToolsHash", "Presentation" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lanes");
        }
    }
}
