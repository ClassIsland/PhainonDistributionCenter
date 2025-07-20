using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhainonDistributionCenter.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicKeyId",
                table: "FileMapInfos",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "PublicKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublicKey = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicKeys", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FileMapInfos_PublicKeyId",
                table: "FileMapInfos",
                column: "PublicKeyId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileMapInfos_PublicKeys_PublicKeyId",
                table: "FileMapInfos",
                column: "PublicKeyId",
                principalTable: "PublicKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileMapInfos_PublicKeys_PublicKeyId",
                table: "FileMapInfos");

            migrationBuilder.DropTable(
                name: "PublicKeys");

            migrationBuilder.DropIndex(
                name: "IX_FileMapInfos_PublicKeyId",
                table: "FileMapInfos");

            migrationBuilder.DropColumn(
                name: "PublicKeyId",
                table: "FileMapInfos");
        }
    }
}
