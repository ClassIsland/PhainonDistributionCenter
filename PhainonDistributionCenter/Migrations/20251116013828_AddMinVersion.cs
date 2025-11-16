using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhainonDistributionCenter.Migrations
{
    /// <inheritdoc />
    public partial class AddMinVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinVersionBuild",
                table: "DistributionInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinVersionMajor",
                table: "DistributionInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinVersionMinor",
                table: "DistributionInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinVersionRevision",
                table: "DistributionInfos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinVersionBuild",
                table: "DistributionInfos");

            migrationBuilder.DropColumn(
                name: "MinVersionMajor",
                table: "DistributionInfos");

            migrationBuilder.DropColumn(
                name: "MinVersionMinor",
                table: "DistributionInfos");

            migrationBuilder.DropColumn(
                name: "MinVersionRevision",
                table: "DistributionInfos");
        }
    }
}
