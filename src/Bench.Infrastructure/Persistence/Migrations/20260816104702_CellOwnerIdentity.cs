using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CellOwnerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerHost",
                table: "cells",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OwnerPid",
                table: "cells",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerHost",
                table: "cells");

            migrationBuilder.DropColumn(
                name: "OwnerPid",
                table: "cells");
        }
    }
}
