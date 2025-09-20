using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhainonDistributionCenter.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DistributionChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileRepoEntries",
                columns: table => new
                {
                    FileSha512 = table.Column<byte[]>(type: "bytea", maxLength: 64, nullable: false),
                    ArchiveSha512 = table.Column<byte[]>(type: "bytea", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ArchiveDownloadUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileRepoEntries", x => x.FileSha512);
                });

            migrationBuilder.CreateTable(
                name: "PublicKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicKeys", x => x.Id);
                    table.UniqueConstraint("AK_PublicKeys_KeyId", x => x.KeyId);
                });

            migrationBuilder.CreateTable(
                name: "VersionInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Codename = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileMapInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    PgpSignature = table.Column<string>(type: "text", nullable: false),
                    PublicKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMapInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileMapInfos_PublicKeys_PublicKeyId",
                        column: x => x.PublicKeyId,
                        principalTable: "PublicKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DistributionInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangeLog = table.Column<string>(type: "text", nullable: false),
                    VersionInfoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionInfos_VersionInfos_VersionInfoId",
                        column: x => x.VersionInfoId,
                        principalTable: "VersionInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DistributionChannelDistributionInfo",
                columns: table => new
                {
                    AssociatedDistributionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionChannelDistributionInfo", x => new { x.AssociatedDistributionsId, x.ChannelsId });
                    table.ForeignKey(
                        name: "FK_DistributionChannelDistributionInfo_DistributionChannels_Ch~",
                        column: x => x.ChannelsId,
                        principalTable: "DistributionChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DistributionChannelDistributionInfo_DistributionInfos_Assoc~",
                        column: x => x.AssociatedDistributionsId,
                        principalTable: "DistributionInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DistributionSubChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Os = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Arch = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Package = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BuildType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FileMapInfoId = table.Column<Guid>(type: "uuid", nullable: false),
                    DistributionInfoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionSubChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionSubChannels_DistributionInfos_DistributionInfoId",
                        column: x => x.DistributionInfoId,
                        principalTable: "DistributionInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DistributionSubChannels_FileMapInfos_FileMapInfoId",
                        column: x => x.FileMapInfoId,
                        principalTable: "FileMapInfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionChannelDistributionInfo_ChannelsId",
                table: "DistributionChannelDistributionInfo",
                column: "ChannelsId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionInfos_VersionInfoId",
                table: "DistributionInfos",
                column: "VersionInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionSubChannels_DistributionInfoId",
                table: "DistributionSubChannels",
                column: "DistributionInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionSubChannels_FileMapInfoId",
                table: "DistributionSubChannels",
                column: "FileMapInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMapInfos_PublicKeyId",
                table: "FileMapInfos",
                column: "PublicKeyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributionChannelDistributionInfo");

            migrationBuilder.DropTable(
                name: "DistributionSubChannels");

            migrationBuilder.DropTable(
                name: "FileRepoEntries");

            migrationBuilder.DropTable(
                name: "DistributionChannels");

            migrationBuilder.DropTable(
                name: "DistributionInfos");

            migrationBuilder.DropTable(
                name: "FileMapInfos");

            migrationBuilder.DropTable(
                name: "VersionInfos");

            migrationBuilder.DropTable(
                name: "PublicKeys");
        }
    }
}
