using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omnijoy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPostingPolicyAndEventPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "Posts",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "PostingPolicy",
                table: "Events",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "OrganizerOnly")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_EventId",
                table: "Posts",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Events_EventId",
                table: "Posts",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Events_EventId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_EventId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "PostingPolicy",
                table: "Events");
        }
    }
}
