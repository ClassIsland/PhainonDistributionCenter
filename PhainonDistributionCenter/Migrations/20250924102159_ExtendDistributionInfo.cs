using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhainonDistributionCenter.Migrations
{
    /// <inheritdoc />
    public partial class ExtendDistributionInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FriendlyVersion",
                table: "DistributionInfos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FriendlyVersionShort",
                table: "DistributionInfos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "DistributionInfos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FriendlyVersion",
                table: "DistributionInfos");

            migrationBuilder.DropColumn(
                name: "FriendlyVersionShort",
                table: "DistributionInfos");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "DistributionInfos");
        }
    }
}
