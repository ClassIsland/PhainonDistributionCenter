using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhainonDistributionCenter.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HourlyDownloadStatistics",
                columns: table => new
                {
                    HourUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DistributionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubChannel = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    RegionCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RegionName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HourlyDownloadStatistics", x => new { x.HourUtc, x.DistributionId, x.Version, x.SubChannel, x.Source, x.RegionCode });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HourlyDownloadStatistics");
        }
    }
}
