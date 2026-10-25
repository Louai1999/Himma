using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Himma.Api.Migrations.Postgres
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
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BodyMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    NeckCm = table.Column<double>(type: "double precision", nullable: true),
                    WaistCm = table.Column<double>(type: "double precision", nullable: true),
                    HipCm = table.Column<double>(type: "double precision", nullable: true),
                    ChestCm = table.Column<double>(type: "double precision", nullable: true),
                    ArmCm = table.Column<double>(type: "double precision", nullable: true),
                    ThighCm = table.Column<double>(type: "double precision", nullable: true),
                    BodyFatPercent = table.Column<double>(type: "double precision", nullable: true),
                    MuscleMassKg = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
