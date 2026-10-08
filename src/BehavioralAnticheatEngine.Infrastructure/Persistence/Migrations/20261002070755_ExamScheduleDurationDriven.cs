using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExamScheduleDurationDriven : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "assessments");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartsAtUtc",
                table: "exam_schedules",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndsAtUtc",
                table: "exam_schedules",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "exam_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing rows already had an explicit Starts/Ends window; derive their
            // duration from it instead of leaving the default 0 (which the domain
            // would reject as invalid once touched again).
            migrationBuilder.Sql(
                """
                UPDATE exam_schedules
                SET "DurationMinutes" = GREATEST(1, CEIL(EXTRACT(epoch FROM ("EndsAtUtc" - "StartsAtUtc")) / 60)::int)
                WHERE "StartsAtUtc" IS NOT NULL AND "EndsAtUtc" IS NOT NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "exam_schedules");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartsAtUtc",
                table: "exam_schedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndsAtUtc",
                table: "exam_schedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "assessments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
