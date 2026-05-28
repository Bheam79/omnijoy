using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omnijoy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUrlSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UrlSlug",
                table: "Users",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UrlSlug",
                table: "CompanyPages",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UrlSlug",
                table: "Users",
                column: "UrlSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPages_UrlSlug",
                table: "CompanyPages",
                column: "UrlSlug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_UrlSlug",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_CompanyPages_UrlSlug",
                table: "CompanyPages");

            migrationBuilder.DropColumn(
                name: "UrlSlug",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UrlSlug",
                table: "CompanyPages");
        }
    }
}
