using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrancProject.Migrations
{
    public partial class InitialBaseline : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Create SDSResults table
            migrationBuilder.CreateTable(
                name: "SDSResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    HollandCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AIFeedback = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SDSResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SDSResults_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SDSResults_UserId",
                table: "SDSResults",
                column: "UserId");

            // 2) Add SDSResultId column to SDSResponses
            migrationBuilder.AddColumn<int>(
                name: "SDSResultId",
                table: "SDSResponses",
                type: "int",
                nullable: true);

            // 3) Create index on SDSResultId
            migrationBuilder.CreateIndex(
                name: "IX_SDSResponses_SDSResultId",
                table: "SDSResponses",
                column: "SDSResultId");

            // 4) Add foreign key (NO ACTION on delete)
            migrationBuilder.AddForeignKey(
                name: "FK_SDSResponses_SDSResults_SDSResultId",
                table: "SDSResponses",
                column: "SDSResultId",
                principalTable: "SDSResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict   // IMPORTANT FIX
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SDSResponses_SDSResults_SDSResultId",
                table: "SDSResponses");

            migrationBuilder.DropIndex(
                name: "IX_SDSResponses_SDSResultId",
                table: "SDSResponses");

            migrationBuilder.DropColumn(
                name: "SDSResultId",
                table: "SDSResponses");

            migrationBuilder.DropTable(
                name: "SDSResults");
        }
    }
}
