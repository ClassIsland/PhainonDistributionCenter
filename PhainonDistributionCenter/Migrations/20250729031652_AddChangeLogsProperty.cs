using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhainonDistributionCenter.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeLogsProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DistributionSubChannels_DistributionInfos_DistributionInfoId",
                table: "DistributionSubChannels");

            migrationBuilder.AlterColumn<Guid>(
                name: "DistributionInfoId",
                table: "DistributionSubChannels",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "ChangeLog",
                table: "DistributionInfos",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionSubChannels_DistributionInfos_DistributionInfoId",
                table: "DistributionSubChannels",
                column: "DistributionInfoId",
                principalTable: "DistributionInfos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DistributionSubChannels_DistributionInfos_DistributionInfoId",
                table: "DistributionSubChannels");

            migrationBuilder.DropColumn(
                name: "ChangeLog",
                table: "DistributionInfos");

            migrationBuilder.AlterColumn<Guid>(
                name: "DistributionInfoId",
                table: "DistributionSubChannels",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionSubChannels_DistributionInfos_DistributionInfoId",
                table: "DistributionSubChannels",
                column: "DistributionInfoId",
                principalTable: "DistributionInfos",
                principalColumn: "Id");
        }
    }
}
