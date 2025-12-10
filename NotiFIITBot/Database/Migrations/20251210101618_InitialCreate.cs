using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NotiFIITBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lessons",
                columns: table => new
                {
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    men_group = table.Column<int>(type: "integer", nullable: false),
                    sub_group = table.Column<int>(type: "integer", nullable: true),
                    evenness = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    pair_number = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    subject_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    teacher_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    classroom_number = table.Column<string>(type: "text", nullable: true),
                    classroom_route_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lessons", x => x.lesson_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    telegram_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_number = table.Column<int>(type: "integer", nullable: true),
                    subgroup_number = table.Column<int>(type: "integer", nullable: true),
                    notifications_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    global_notification_minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.telegram_id);
                });

            migrationBuilder.CreateTable(
                name: "week_evenness_configs",
                columns: table => new
                {
                    evenness = table.Column<int>(type: "integer", nullable: false),
                    first_monday = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_week_evenness_configs", x => x.evenness);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_config",
                columns: table => new
                {
                    telegram_id = table.Column<long>(type: "bigint", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_notification_enabled_override = table.Column<bool>(type: "boolean", nullable: true),
                    notification_minutes_override = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notification_config", x => new { x.telegram_id, x.lesson_id });
                    table.ForeignKey(
                        name: "FK_user_notification_config_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lessons",
                        principalColumn: "lesson_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_notification_config_users_telegram_id",
                        column: x => x.telegram_id,
                        principalTable: "users",
                        principalColumn: "telegram_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_notification_config_lesson_id",
                table: "user_notification_config",
                column: "lesson_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_notification_config");

            migrationBuilder.DropTable(
                name: "week_evenness_configs");

            migrationBuilder.DropTable(
                name: "lessons");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
