using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Himma.Api.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddCoachPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoachAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Bio = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    InviteCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MaxClients = table.Column<int>(type: "integer", nullable: false),
                    ActiveUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachAccounts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CoachUserId = table.Column<string>(type: "text", nullable: false),
                    ClientUserId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrivateNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachClients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachClients_AspNetUsers_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoachClients_AspNetUsers_CoachUserId",
                        column: x => x.CoachUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CoachUserId = table.Column<string>(type: "text", nullable: false),
                    ClientUserId = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachNotes_AspNetUsers_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CoachNotes_AspNetUsers_CoachUserId",
                        column: x => x.CoachUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoachAccounts_InviteCode",
                table: "CoachAccounts",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachAccounts_UserId",
                table: "CoachAccounts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachClients_ClientUserId_Status",
                table: "CoachClients",
                columns: new[] { "ClientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CoachClients_CoachUserId_Status",
                table: "CoachClients",
                columns: new[] { "CoachUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CoachNotes_ClientUserId_CreatedAtUtc",
                table: "CoachNotes",
                columns: new[] { "ClientUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CoachNotes_CoachUserId_ClientUserId",
                table: "CoachNotes",
                columns: new[] { "CoachUserId", "ClientUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoachAccounts");

            migrationBuilder.DropTable(
                name: "CoachClients");

            migrationBuilder.DropTable(
                name: "CoachNotes");
        }
    }
}
