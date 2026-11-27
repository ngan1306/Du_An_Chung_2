using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KissAppTussing.Migrations
{
    /// <inheritdoc />
    public partial class ChatRoomsAndCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RoomType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Group"),
                    CreatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRooms_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CallSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CallerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAt = table.Column<long>(type: "bigint", nullable: false),
                    AnsweredAt = table.Column<long>(type: "bigint", nullable: true),
                    EndedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallSessions_ChatRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CallSessions_Users_CallerUserId",
                        column: x => x.CallerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CallSessions_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatRoomMembers",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Member"),
                    JoinedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRoomMembers", x => new { x.RoomId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ChatRoomMembers_ChatRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatRoomMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatRoomMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRoomMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRoomMessages_ChatRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatRoomMessages_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CallParticipants",
                columns: table => new
                {
                    CallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    JoinedAt = table.Column<long>(type: "bigint", nullable: false),
                    LeftAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallParticipants", x => new { x.CallId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CallParticipants_CallSessions_CallId",
                        column: x => x.CallId,
                        principalTable: "CallSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CallParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallParticipants_UserId",
                table: "CallParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CallSessions_CallerUserId_StartedAt",
                table: "CallSessions",
                columns: new[] { "CallerUserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CallSessions_RecipientUserId",
                table: "CallSessions",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CallSessions_RoomId",
                table: "CallSessions",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_UserId",
                table: "ChatRoomMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMessages_RoomId_SentAt_Id",
                table: "ChatRoomMessages",
                columns: new[] { "RoomId", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMessages_SenderId",
                table: "ChatRoomMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_CreatedAt",
                table: "ChatRooms",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_CreatorUserId",
                table: "ChatRooms",
                column: "CreatorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallParticipants");

            migrationBuilder.DropTable(
                name: "ChatRoomMembers");

            migrationBuilder.DropTable(
                name: "ChatRoomMessages");

            migrationBuilder.DropTable(
                name: "CallSessions");

            migrationBuilder.DropTable(
                name: "ChatRooms");
        }
    }
}
