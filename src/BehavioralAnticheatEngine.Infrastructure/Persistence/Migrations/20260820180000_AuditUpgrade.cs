using System;
using BehavioralAnticheatEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260820180000_AuditUpgrade")]
    public partial class AuditUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "QuestionId",
                table: "behavioral_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeatureCountsJson",
                table: "session_feature_aggregates",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateIndex(
                name: "IX_behavioral_events_SessionId_QuestionId",
                table: "behavioral_events",
                columns: new[] { "SessionId", "QuestionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_behavioral_events_SessionId_QuestionId",
                table: "behavioral_events");

            migrationBuilder.DropColumn(
                name: "QuestionId",
                table: "behavioral_events");

            migrationBuilder.DropColumn(
                name: "FeatureCountsJson",
                table: "session_feature_aggregates");
        }
    }
}
