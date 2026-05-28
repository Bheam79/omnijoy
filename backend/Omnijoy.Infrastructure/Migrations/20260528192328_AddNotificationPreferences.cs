using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omnijoy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LikesOnMyPosts = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    CommentsOnMyPosts = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    PostShares = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    FriendRequests = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    NewFollower = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Mentions = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    NewPostsFromFriends = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    FamilyRelations = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    EventInvites = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    DirectMessages = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    LiveStreams = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    CompanyPageInvites = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Back-fill a default ("everything on") row for every existing user
            // so the GET endpoint never has to lazy-create one for legacy accounts.
            migrationBuilder.Sql(
                @"INSERT INTO `NotificationPreferences` (`UserId`)
                  SELECT u.`Id` FROM `Users` u
                  WHERE NOT EXISTS (
                      SELECT 1 FROM `NotificationPreferences` p WHERE p.`UserId` = u.`Id`
                  );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationPreferences");
        }
    }
}
