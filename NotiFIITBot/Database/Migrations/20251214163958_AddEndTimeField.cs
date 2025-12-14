using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotiFIITBot.Migrations
{
    /// <inheritdoc />
    public partial class AddEndTimeField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "end_time",
                table: "lessons",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "end_time",
                table: "lessons");
        }
    }
}
