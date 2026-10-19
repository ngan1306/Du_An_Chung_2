using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KissAppTussing.Migrations
{
    /// <inheritdoc />
    public partial class JourneyPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JourneyPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    MediaUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JourneyPosts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JourneyPosts_CreatedAt_Id",
                table: "JourneyPosts",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_JourneyPosts_UserId",
                table: "JourneyPosts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JourneyPosts");
        }
    }
}
