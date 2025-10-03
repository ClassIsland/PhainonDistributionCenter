using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhainonDistributionCenter.Migrations
{
    /// <inheritdoc />
    public partial class SplitVersionStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VersionBuild",
                table: "DistributionInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VersionMajor",
                table: "DistributionInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VersionMinor",
                table: "DistributionInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VersionRevision",
                table: "DistributionInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VersionBuild",
                table: "DistributionInfos");

            migrationBuilder.DropColumn(
                name: "VersionMajor",
                table: "DistributionInfos");

            migrationBuilder.DropColumn(
                name: "VersionMinor",
                table: "DistributionInfos");

            migrationBuilder.DropColumn(
                name: "VersionRevision",
                table: "DistributionInfos");
        }
    }
}
