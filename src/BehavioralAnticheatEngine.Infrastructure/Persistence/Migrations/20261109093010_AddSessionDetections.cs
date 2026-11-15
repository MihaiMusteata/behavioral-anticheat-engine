using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionDetections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_detections",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Score = table.Column<double>(type: "double precision", nullable: true),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VotersUsed = table.Column<int>(type: "integer", nullable: true),
                    ModelsJson = table.Column<string>(type: "jsonb", nullable: false),
                    QuestionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    QuestionSummaryJson = table.Column<string>(type: "jsonb", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_detections", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_session_detections_exam_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "exam_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_session_detections_ExamScheduleId",
                table: "session_detections",
                column: "ExamScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_session_detections_Status_RequestedAtUtc",
                table: "session_detections",
                columns: new[] { "Status", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_detections");
        }
    }
}
