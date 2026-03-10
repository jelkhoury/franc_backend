using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrancProject.Migrations
{
    /// <inheritdoc />
    public partial class NewColumnsSDSIsCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "SDSResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SDSResults",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "SDSResults",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "SDSResults");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "SDSResults");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "SDSResults");
        }
    }
}
