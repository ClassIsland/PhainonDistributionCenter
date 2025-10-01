using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhainonDistributionCenter.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatorUid = table.Column<int>(type: "integer", nullable: false),
                    CreatorName = table.Column<string>(type: "text", nullable: false),
                    ExpireTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessTokens", x => x.Id);
                    table.UniqueConstraint("AK_AccessTokens_TokenHash", x => x.TokenHash);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessTokens");
        }
    }
}
