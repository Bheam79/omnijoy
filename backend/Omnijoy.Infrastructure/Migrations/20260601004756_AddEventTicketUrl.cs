using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omnijoy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventTicketUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TicketUrl",
                table: "Events",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TicketUrl",
                table: "Events");
        }
    }
}
