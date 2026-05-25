using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrancProject.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationReportSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvaluationReportSkills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluationReportId = table.Column<int>(type: "int", nullable: false),
                    SkillCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SkillName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationReportSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationReportSkills_EvaluationReports_EvaluationReportId",
                        column: x => x.EvaluationReportId,
                        principalTable: "EvaluationReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReportSkills_EvaluationReportId_SkillCode",
                table: "EvaluationReportSkills",
                columns: new[] { "EvaluationReportId", "SkillCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationReportSkills");
        }
    }
}
