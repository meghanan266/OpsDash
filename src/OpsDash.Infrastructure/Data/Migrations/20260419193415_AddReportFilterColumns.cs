using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDash.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportFilterColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FilterEndDate",
                table: "Reports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FilterStartDate",
                table: "Reports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceIncidentId",
                table: "Reports",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilterEndDate",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "FilterStartDate",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "SourceIncidentId",
                table: "Reports");
        }
    }
}
