using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VariantCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VariantId",
                table: "cells",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantName",
                table: "cells",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_variants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cells_RunId_VariantId_State",
                table: "cells",
                columns: new[] { "RunId", "VariantId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_cells_VariantId",
                table: "cells",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_variants_Hash",
                table: "variants",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_variants_Name",
                table: "variants",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_cells_variants_VariantId",
                table: "cells",
                column: "VariantId",
                principalTable: "variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cells_variants_VariantId",
                table: "cells");

            migrationBuilder.DropTable(
                name: "variants");

            migrationBuilder.DropIndex(
                name: "IX_cells_RunId_VariantId_State",
                table: "cells");

            migrationBuilder.DropIndex(
                name: "IX_cells_VariantId",
                table: "cells");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "cells");

            migrationBuilder.DropColumn(
                name: "VariantName",
                table: "cells");
        }
    }
}
