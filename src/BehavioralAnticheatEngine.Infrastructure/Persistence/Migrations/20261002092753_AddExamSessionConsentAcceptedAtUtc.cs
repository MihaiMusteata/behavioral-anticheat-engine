using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamSessionConsentAcceptedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsentAcceptedAtUtc",
                table: "exam_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Sessions created before this feature existed had no consent capture step.
            // Backfill with their own start time as the closest honest approximation,
            // rather than leaving the sentinel default (year 1) on historical rows.
            migrationBuilder.Sql(
                """
                UPDATE exam_sessions
                SET "ConsentAcceptedAtUtc" = "StartedAtUtc"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentAcceptedAtUtc",
                table: "exam_sessions");
        }
    }
}
