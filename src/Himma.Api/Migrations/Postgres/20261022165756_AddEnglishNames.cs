using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Himma.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddEnglishNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Foods",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServingLabelEn",
                table: "Foods",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentEn",
                table: "Exercises",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstructionsEn",
                table: "Exercises",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Exercises",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "ServingLabelEn",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "EquipmentEn",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "InstructionsEn",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Exercises");
        }
    }
}
