using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KissAppTussing.Migrations
{
    /// <inheritdoc />
    public partial class JourneyInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JourneyComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JourneyComments_JourneyPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "JourneyPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JourneyComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JourneyPostLikes",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyPostLikes", x => new { x.PostId, x.UserId });
                    table.ForeignKey(
                        name: "FK_JourneyPostLikes_JourneyPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "JourneyPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JourneyPostLikes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JourneyComments_PostId_CreatedAt",
                table: "JourneyComments",
                columns: new[] { "PostId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JourneyComments_UserId",
                table: "JourneyComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_JourneyPostLikes_UserId",
                table: "JourneyPostLikes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JourneyComments");

            migrationBuilder.DropTable(
                name: "JourneyPostLikes");
        }
    }
}
