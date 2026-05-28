using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omnijoy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new Role column with a default of "User".
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "User")
                .Annotation("MySql:CharSet", "utf8mb4");

            // 2. Promote existing admins: anyone with IsAdmin = 1 becomes "Admin".
            migrationBuilder.Sql("UPDATE `Users` SET `Role` = 'Admin' WHERE `IsAdmin` = 1;");

            // 3. Drop the old boolean column now that the data is migrated.
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Re-add the IsAdmin boolean column (default false).
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // 2. Restore IsAdmin = 1 for any Admin-role users.
            migrationBuilder.Sql("UPDATE `Users` SET `IsAdmin` = 1 WHERE `Role` = 'Admin';");

            // 3. Drop the Role column.
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }
    }
}
