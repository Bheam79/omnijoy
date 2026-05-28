using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omnijoy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FULLTEXT indexes for search — MySQL/MariaDB only.
            // The search service uses LIKE-based queries (compatible with all providers),
            // while these indexes improve performance on production MySQL via MATCH AGAINST.
            migrationBuilder.Sql(
                "ALTER TABLE `Users` ADD FULLTEXT INDEX `FT_Users_DisplayName_Bio` (`DisplayName`, `Bio`);");

            migrationBuilder.Sql(
                "ALTER TABLE `Posts` ADD FULLTEXT INDEX `FT_Posts_Content` (`Content`);");

            migrationBuilder.Sql(
                "ALTER TABLE `Events` ADD FULLTEXT INDEX `FT_Events_Title_Description` (`Title`, `Description`);");

            migrationBuilder.Sql(
                "ALTER TABLE `CompanyPages` ADD FULLTEXT INDEX `FT_CompanyPages_Name_Description` (`Name`, `Description`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `Users` DROP INDEX `FT_Users_DisplayName_Bio`;");
            migrationBuilder.Sql("ALTER TABLE `Posts` DROP INDEX `FT_Posts_Content`;");
            migrationBuilder.Sql("ALTER TABLE `Events` DROP INDEX `FT_Events_Title_Description`;");
            migrationBuilder.Sql("ALTER TABLE `CompanyPages` DROP INDEX `FT_CompanyPages_Name_Description`;");
        }
    }
}
