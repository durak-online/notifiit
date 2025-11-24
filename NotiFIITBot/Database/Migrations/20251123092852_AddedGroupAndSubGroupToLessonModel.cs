using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotiFIITBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddedGroupAndSubGroupToLessonModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "men_group",
                table: "lessons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sub_group",
                table: "lessons",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "men_group",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "sub_group",
                table: "lessons");
        }
    }
}
