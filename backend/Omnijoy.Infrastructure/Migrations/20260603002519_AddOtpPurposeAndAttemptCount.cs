using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omnijoy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpPurposeAndAttemptCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_Email_IsUsed_ExpiresAt",
                table: "OtpCodes");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "OtpCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "OtpCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_Email_Purpose_IsUsed_ExpiresAt",
                table: "OtpCodes",
                columns: new[] { "Email", "Purpose", "IsUsed", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_Email_Purpose_IsUsed_ExpiresAt",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "OtpCodes");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_Email_IsUsed_ExpiresAt",
                table: "OtpCodes",
                columns: new[] { "Email", "IsUsed", "ExpiresAt" });
        }
    }
}
