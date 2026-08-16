using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bench.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "question_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reviewers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviewers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "run_questions",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<string>(type: "text", nullable: false),
                    GroupKey = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_questions", x => new { x.RunId, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_run_questions_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bank_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    CodeTaskJson = table.Column<string>(type: "text", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    ReferenceAnswer = table.Column<string>(type: "text", nullable: false),
                    ExpectationsJson = table.Column<string>(type: "jsonb", nullable: false),
                    TargetRepoUrl = table.Column<string>(type: "text", nullable: false),
                    AuthoredAtCommit = table.Column<string>(type: "text", nullable: false),
                    SourceKind = table.Column<string>(type: "text", nullable: false),
                    AuthorModel = table.Column<string>(type: "text", nullable: false),
                    SeedKind = table.Column<string>(type: "text", nullable: false),
                    SeedReference = table.Column<string>(type: "text", nullable: false),
                    SeedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bank_questions_question_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "question_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_group_moves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromGroup = table.Column<string>(type: "text", nullable: false),
                    ToGroup = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_group_moves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_group_moves_bank_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "bank_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_reviews_bank_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "bank_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_question_reviews_reviewers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "reviewers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_questions_GroupId_Ordinal",
                table: "bank_questions",
                columns: new[] { "GroupId", "Ordinal" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_questions_QuestionId",
                table: "bank_questions",
                column: "QuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_questions_State",
                table: "bank_questions",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_question_group_moves_QuestionId_At",
                table: "question_group_moves",
                columns: new[] { "QuestionId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_question_groups_Key",
                table: "question_groups",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_reviews_QuestionId_ReviewerId",
                table: "question_reviews",
                columns: new[] { "QuestionId", "ReviewerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_reviews_ReviewerId",
                table: "question_reviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_reviewers_Key",
                table: "reviewers",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_run_questions_RunId_GroupKey",
                table: "run_questions",
                columns: new[] { "RunId", "GroupKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_group_moves");

            migrationBuilder.DropTable(
                name: "question_reviews");

            migrationBuilder.DropTable(
                name: "run_questions");

            migrationBuilder.DropTable(
                name: "bank_questions");

            migrationBuilder.DropTable(
                name: "reviewers");

            migrationBuilder.DropTable(
                name: "question_groups");
        }
    }
}
