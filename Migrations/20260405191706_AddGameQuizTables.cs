using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FrancProject.Migrations
{
    /// <inheritdoc />
    public partial class AddGameQuizTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameLevels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LevelNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BadgeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PassScore = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGameProgresses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CurrentLevel = table.Column<int>(type: "int", nullable: false),
                    HighestUnlockedLevel = table.Column<int>(type: "int", nullable: false),
                    BronzeBadgeEarned = table.Column<bool>(type: "bit", nullable: false),
                    SilverBadgeEarned = table.Column<bool>(type: "bit", nullable: false),
                    GoldBadgeEarned = table.Column<bool>(type: "bit", nullable: false),
                    PlatinumBadgeEarned = table.Column<bool>(type: "bit", nullable: false),
                    DiamondBadgeEarned = table.Column<bool>(type: "bit", nullable: false),
                    TotalPoints = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGameProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGameProgresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameQuestions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LevelId = table.Column<long>(type: "bigint", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionA = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OptionB = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OptionC = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OptionD = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrectOption = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Hint = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameQuestions_GameLevels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "GameLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LevelId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Score = table.Column<int>(type: "int", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "int", nullable: false),
                    WrongAnswers = table.Column<int>(type: "int", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_GameLevels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "GameLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameSessionAnswers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    QuestionId = table.Column<long>(type: "bigint", nullable: false),
                    QuestionOrder = table.Column<int>(type: "int", nullable: false),
                    AnswerAttemptCount = table.Column<int>(type: "int", nullable: false),
                    SelectedOption = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    UsedSkip = table.Column<bool>(type: "bit", nullable: false),
                    UsedFiftyFifty = table.Column<bool>(type: "bit", nullable: false),
                    UsedDoubleChance = table.Column<bool>(type: "bit", nullable: false),
                    UsedTimeFreeze = table.Column<bool>(type: "bit", nullable: false),
                    UsedHint = table.Column<bool>(type: "bit", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessionAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessionAnswers_GameQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "GameQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameSessionAnswers_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "GameLevels",
                columns: new[] { "Id", "BadgeName", "IsActive", "LevelNumber", "Name", "PassScore" },
                values: new object[,]
                {
                    { 1L, "Bronze", true, 1, "Level 1", 7 },
                    { 2L, "Silver", true, 2, "Level 2", 7 },
                    { 3L, "Gold", true, 3, "Level 3", 7 },
                    { 4L, "Platinum", true, 4, "Level 4", 7 },
                    { 5L, "Diamond", true, 5, "Level 5", 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameLevels_LevelNumber",
                table: "GameLevels",
                column: "LevelNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameQuestions_LevelId_IsActive",
                table: "GameQuestions",
                columns: new[] { "LevelId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessionAnswers_QuestionId",
                table: "GameSessionAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessionAnswers_SessionId_QuestionOrder",
                table: "GameSessionAnswers",
                columns: new[] { "SessionId", "QuestionOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_LevelId",
                table: "GameSessions",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_UserId_InProgress_Unique",
                table: "GameSessions",
                column: "UserId",
                unique: true,
                filter: "[Status] = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "IX_UserGameProgresses_UserId",
                table: "UserGameProgresses",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSessionAnswers");

            migrationBuilder.DropTable(
                name: "UserGameProgresses");

            migrationBuilder.DropTable(
                name: "GameQuestions");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropTable(
                name: "GameLevels");
        }
    }
}
