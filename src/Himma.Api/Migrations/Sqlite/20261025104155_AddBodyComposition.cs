using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Himma.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddBodyComposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TargetBodyFatPercent",
                table: "UserProfiles",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BodyMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    NeckCm = table.Column<double>(type: "REAL", nullable: true),
                    WaistCm = table.Column<double>(type: "REAL", nullable: true),
                    HipCm = table.Column<double>(type: "REAL", nullable: true),
                    ChestCm = table.Column<double>(type: "REAL", nullable: true),
                    ArmCm = table.Column<double>(type: "REAL", nullable: true),
                    ThighCm = table.Column<double>(type: "REAL", nullable: true),
                    BodyFatPercent = table.Column<double>(type: "REAL", nullable: true),
                    MuscleMassKg = table.Column<double>(type: "REAL", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BodyMeasurements_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BodyMeasurements_UserId_Date",
                table: "BodyMeasurements",
                columns: new[] { "UserId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BodyMeasurements");

            migrationBuilder.DropColumn(
                name: "TargetBodyFatPercent",
                table: "UserProfiles");
        }
    }
}
