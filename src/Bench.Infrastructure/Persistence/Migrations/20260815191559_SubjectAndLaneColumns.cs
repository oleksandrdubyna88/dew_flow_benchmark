using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubjectAndLaneColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LaneName",
                table: "cells",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubjectModelId",
                table: "cells",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LaneName",
                table: "cells");

            migrationBuilder.DropColumn(
                name: "SubjectModelId",
                table: "cells");
        }
    }
}
