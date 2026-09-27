using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuestExamAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_enrollments");

            migrationBuilder.AddColumn<string>(
                name: "ParticipantName",
                table: "exam_sessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccessPinCode",
                table: "exam_schedules",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            // Existing rows all get the same "" default above, which would collide
            // under the unique index created next. Backfill each with a distinct
            // random 6-digit code first (collisions across a handful of rows are
            // astronomically unlikely; this only runs once, for pre-existing data).
            migrationBuilder.Sql(
                """
                UPDATE exam_schedules
                SET "AccessPinCode" = lpad((floor(random() * 1000000))::text, 6, '0')
                """);

            migrationBuilder.CreateIndex(
                name: "IX_exam_schedules_AccessPinCode",
                table: "exam_schedules",
                column: "AccessPinCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_exam_schedules_AccessPinCode",
                table: "exam_schedules");

            migrationBuilder.DropColumn(
                name: "ParticipantName",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "AccessPinCode",
                table: "exam_schedules");

            migrationBuilder.CreateTable(
                name: "exam_enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_enrollments_exam_schedules_ExamScheduleId",
                        column: x => x.ExamScheduleId,
                        principalTable: "exam_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exam_enrollments_ExamScheduleId_StudentId",
                table: "exam_enrollments",
                columns: new[] { "ExamScheduleId", "StudentId" },
                unique: true);
        }
    }
}
