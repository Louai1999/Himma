using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Himma.Api.Migrations.Sqlite
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
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServingLabelEn",
                table: "Foods",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentEn",
                table: "Exercises",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstructionsEn",
                table: "Exercises",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Exercises",
                type: "TEXT",
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
