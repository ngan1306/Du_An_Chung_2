using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KissAppTussing.Migrations
{
    /// <inheritdoc />
    public partial class ProfileDiscoveryPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Users",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Education",
                table: "Users",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Users",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "PreferNotToSay");

            migrationBuilder.AddColumn<string>(
                name: "LookingFor",
                table: "Users",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Dating");

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "Users",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TermsAcceptedAt",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Interests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfilePhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsAvatar = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfilePhotos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserInterests",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InterestId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInterests", x => new { x.UserId, x.InterestId });
                    table.ForeignKey(
                        name: "FK_UserInterests_Interests_InterestId",
                        column: x => x.InterestId,
                        principalTable: "Interests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserInterests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Interests_Name",
                table: "Interests",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePhotos_UserId_IsAvatar",
                table: "ProfilePhotos",
                columns: new[] { "UserId", "IsAvatar" },
                unique: true,
                filter: "\"IsAvatar\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserInterests_InterestId",
                table: "UserInterests",
                column: "InterestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfilePhotos");

            migrationBuilder.DropTable(
                name: "UserInterests");

            migrationBuilder.DropTable(
                name: "Interests");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Education",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LookingFor",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "Users");
        }
    }
}
