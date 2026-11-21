using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizLive.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameLoop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: false),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: false),
                    StreakAtAnswer = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_answers_game_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "game_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_answers_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_answers_session_options_SelectedOptionId",
                        column: x => x.SelectedOptionId,
                        principalTable: "session_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_answers_session_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "session_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_answers_PlayerId_QuestionId",
                table: "answers",
                columns: new[] { "PlayerId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_answers_QuestionId",
                table: "answers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_answers_SelectedOptionId",
                table: "answers",
                column: "SelectedOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_answers_SessionId_PlayerId",
                table: "answers",
                columns: new[] { "SessionId", "PlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_answers_SessionId_QuestionId",
                table: "answers",
                columns: new[] { "SessionId", "QuestionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answers");
        }
    }
}
