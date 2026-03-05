using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashFlow.Consolidation.API.Migrations
{
    /// <inheritdoc />
    public partial class SwitchToXminRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "consolidation",
                table: "daily_summary");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "consolidation",
                table: "daily_summary",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "consolidation",
                table: "daily_summary");

            migrationBuilder.AddColumn<long>(
                name: "row_version",
                schema: "consolidation",
                table: "daily_summary",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
