using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NotiFIITBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class DebugCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "evenness",
                table: "lessons",
                newName: "parity");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:evenness", "even,odd,always");

            migrationBuilder.AlterColumn<int>(
                name: "lesson_id",
                table: "lessons",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "parity",
                table: "lessons",
                newName: "evenness");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:evenness", "even,odd,always");

            migrationBuilder.AlterColumn<int>(
                name: "lesson_id",
                table: "lessons",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
