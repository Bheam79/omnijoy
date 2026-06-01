using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omnijoy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationAddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationCity",
                table: "Users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LocationCountry",
                table: "Users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LocationCountryCode",
                table: "Users",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "LocationLatitude",
                table: "Users",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LocationLongitude",
                table: "Users",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "Users",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LocationPlaceId",
                table: "Users",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LocationCity",
                table: "Events",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LocationCountry",
                table: "Events",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "LocationLatitude",
                table: "Events",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LocationLongitude",
                table: "Events",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationPlaceId",
                table: "Events",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AddressCity",
                table: "CompanyPages",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AddressCountry",
                table: "CompanyPages",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "AddressLatitude",
                table: "CompanyPages",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AddressLongitude",
                table: "CompanyPages",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressPlaceId",
                table: "CompanyPages",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AddressText",
                table: "CompanyPages",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationCity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationCountry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationCountryCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationLatitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationPlaceId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationCity",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LocationCountry",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LocationLatitude",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LocationPlaceId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "AddressCity",
                table: "CompanyPages");

            migrationBuilder.DropColumn(
                name: "AddressCountry",
                table: "CompanyPages");

            migrationBuilder.DropColumn(
                name: "AddressLatitude",
                table: "CompanyPages");

            migrationBuilder.DropColumn(
                name: "AddressLongitude",
                table: "CompanyPages");

            migrationBuilder.DropColumn(
                name: "AddressPlaceId",
                table: "CompanyPages");

            migrationBuilder.DropColumn(
                name: "AddressText",
                table: "CompanyPages");
        }
    }
}
