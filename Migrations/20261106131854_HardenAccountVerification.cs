using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrancProject.Migrations
{
    /// <inheritdoc />
    public partial class HardenAccountVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VerificationCodeAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationCodeExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationCodePurpose",
                table: "Users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationCodeSentAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerificationCodeVersion",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationCodeAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VerificationCodeExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VerificationCodePurpose",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VerificationCodeSentAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VerificationCodeVersion",
                table: "Users");
        }
    }
}
