using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToAssessmentAndExamSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "exam_schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "exam_schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "assessments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_exam_schedules_IsDeleted",
                table: "exam_schedules",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_assessments_IsDeleted",
                table: "assessments",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_exam_schedules_IsDeleted",
                table: "exam_schedules");

            migrationBuilder.DropIndex(
                name: "IX_assessments_IsDeleted",
                table: "assessments");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "assessments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "assessments");
        }
    }
}
