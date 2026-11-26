using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KissAppTussing.Migrations
{
    /// <inheritdoc />
    public partial class FixSocialModelConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConnectionRequests_Users_FirstUserId",
                table: "ConnectionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ConnectionRequests_Users_RequestedByUserId",
                table: "ConnectionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ConnectionRequests_Users_SecondUserId",
                table: "ConnectionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Users_FirstUserId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Users_SecondUserId",
                table: "Conversations");

            migrationBuilder.AddForeignKey(
                name: "FK_ConnectionRequests_Users_FirstUserId",
                table: "ConnectionRequests",
                column: "FirstUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConnectionRequests_Users_RequestedByUserId",
                table: "ConnectionRequests",
                column: "RequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConnectionRequests_Users_SecondUserId",
                table: "ConnectionRequests",
                column: "SecondUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Users_FirstUserId",
                table: "Conversations",
                column: "FirstUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Users_SecondUserId",
                table: "Conversations",
                column: "SecondUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConnectionRequests_Users_FirstUserId",
                table: "ConnectionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ConnectionRequests_Users_RequestedByUserId",
                table: "ConnectionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ConnectionRequests_Users_SecondUserId",
                table: "ConnectionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Users_FirstUserId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Users_SecondUserId",
                table: "Conversations");

            migrationBuilder.AddForeignKey(
                name: "FK_ConnectionRequests_Users_FirstUserId",
                table: "ConnectionRequests",
                column: "FirstUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConnectionRequests_Users_RequestedByUserId",
                table: "ConnectionRequests",
                column: "RequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConnectionRequests_Users_SecondUserId",
                table: "ConnectionRequests",
                column: "SecondUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Users_FirstUserId",
                table: "Conversations",
                column: "FirstUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Users_SecondUserId",
                table: "Conversations",
                column: "SecondUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
