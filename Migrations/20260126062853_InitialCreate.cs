using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrancProject.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================
            // JobComparisonCriteria
            // ============================
            migrationBuilder.CreateTable(
                name: "JobComparisonCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),

                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Section = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),

                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),

                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobComparisonCriteria", x => x.Id);
                });

            // ============================
            // JobComparisons
            // ============================
            migrationBuilder.CreateTable(
                name: "JobComparisons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),

                    UserId = table.Column<int>(type: "int", nullable: false),

                    JobAName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JobBName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),

                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),

                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobComparisons", x => x.Id);

                    table.ForeignKey(
                        name: "FK_JobComparisons_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================
            // JobComparisonAnswers
            // ============================
            migrationBuilder.CreateTable(
                name: "JobComparisonAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),

                    JobComparisonId = table.Column<int>(type: "int", nullable: false),
                    CriterionId = table.Column<int>(type: "int", nullable: false),

                    Weight = table.Column<int>(type: "int", nullable: false),
                    ScoreA = table.Column<int>(type: "int", nullable: false),
                    ScoreB = table.Column<int>(type: "int", nullable: false),

                    NotApplicable = table.Column<bool>(type: "bit", nullable: false),

                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobComparisonAnswers", x => x.Id);

                    table.ForeignKey(
                        name: "FK_JobComparisonAnswers_JobComparisons_JobComparisonId",
                        column: x => x.JobComparisonId,
                        principalTable: "JobComparisons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_JobComparisonAnswers_JobComparisonCriteria_CriterionId",
                        column: x => x.CriterionId,
                        principalTable: "JobComparisonCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================
            // Indexes
            // ============================
            migrationBuilder.CreateIndex(
                name: "IX_JobComparisons_UserId",
                table: "JobComparisons",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobComparisonAnswers_JobComparisonId",
                table: "JobComparisonAnswers",
                column: "JobComparisonId");

            migrationBuilder.CreateIndex(
                name: "IX_JobComparisonAnswers_CriterionId",
                table: "JobComparisonAnswers",
                column: "CriterionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "JobComparisonAnswers");
            migrationBuilder.DropTable(name: "JobComparisons");
            migrationBuilder.DropTable(name: "JobComparisonCriteria");
        }
    }
}
