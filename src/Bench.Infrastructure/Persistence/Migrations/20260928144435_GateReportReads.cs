using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GateReportReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gate_suite_tasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SuiteStamp = table.Column<string>(type: "text", nullable: false),
                    TaskId = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    IsCalibration = table.Column<bool>(type: "boolean", nullable: false),
                    Hosts = table.Column<string>(type: "text", nullable: false),
                    SeedIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    SeedCrossEpic = table.Column<List<bool>>(type: "boolean[]", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gate_suite_tasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gate_suite_tasks_SuiteStamp_TaskId",
                table: "gate_suite_tasks",
                columns: new[] { "SuiteStamp", "TaskId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gate_suite_tasks");
        }
    }
}
