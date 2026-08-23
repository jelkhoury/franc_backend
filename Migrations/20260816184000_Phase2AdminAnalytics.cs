using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrancProject.Migrations
{
    /// <inheritdoc />
    public partial class Phase2AdminAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Faculty",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Major",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ServiceKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResultSummary = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobMatchingSearches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SearchType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Faculty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Major = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QueryText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResultsCount = table.Column<int>(type: "int", nullable: false),
                    SearchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobMatchingSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobMatchingSearches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_OccurredAt",
                table: "ActivityEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_ServiceKey_EntityId_ActivityType",
                table: "ActivityEvents",
                columns: new[] { "ServiceKey", "EntityId", "ActivityType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_ServiceKey_OccurredAt",
                table: "ActivityEvents",
                columns: new[] { "ServiceKey", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_UserId_OccurredAt",
                table: "ActivityEvents",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobMatchingSearches_SearchedAt",
                table: "JobMatchingSearches",
                column: "SearchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobMatchingSearches_UserId",
                table: "JobMatchingSearches",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEvents");

            migrationBuilder.DropTable(
                name: "JobMatchingSearches");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Major",
                table: "Users");
        }
    }
}
