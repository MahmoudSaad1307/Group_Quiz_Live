using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizLive.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionsAndPlayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pin = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PausedFromStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    PausedRemainingMs = table.Column<int>(type: "integer", nullable: true),
                    CurrentQuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false),
                    QuestionOrder = table.Column<string>(type: "jsonb", nullable: false),
                    OptionOrder = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_game_sessions_admins_HostId",
                        column: x => x.HostId,
                        principalTable: "admins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_game_sessions_quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NicknameNormalized = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                    CumulativeResponseTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JoinedAtQuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    IsKicked = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_players_game_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "game_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Explanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TimeLimitSeconds = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_questions_game_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "game_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_options_session_questions_SessionQuestionId",
                        column: x => x.SessionQuestionId,
                        principalTable: "session_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_HostId",
                table: "game_sessions",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_Pin",
                table: "game_sessions",
                column: "Pin",
                unique: true,
                filter: "\"Status\" NOT IN ('Finished', 'Abandoned')");

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_QuizId",
                table: "game_sessions",
                column: "QuizId");

            migrationBuilder.CreateIndex(
                name: "IX_players_SessionId_NicknameNormalized",
                table: "players",
                columns: new[] { "SessionId", "NicknameNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_players_SessionId_TokenHash",
                table: "players",
                columns: new[] { "SessionId", "TokenHash" });

            migrationBuilder.CreateIndex(
                name: "IX_session_options_SessionQuestionId_OrderIndex",
                table: "session_options",
                columns: new[] { "SessionQuestionId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_session_questions_SessionId_OrderIndex",
                table: "session_questions",
                columns: new[] { "SessionId", "OrderIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropTable(
                name: "session_options");

            migrationBuilder.DropTable(
                name: "session_questions");

            migrationBuilder.DropTable(
                name: "game_sessions");
        }
    }
}
