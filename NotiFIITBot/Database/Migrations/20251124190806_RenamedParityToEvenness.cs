using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotiFIITBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class RenamedParityToEvenness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "parity",
                table: "lessons",
                newName: "evenness");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "evenness",
                table: "lessons",
                newName: "parity");
        }
    }
}
