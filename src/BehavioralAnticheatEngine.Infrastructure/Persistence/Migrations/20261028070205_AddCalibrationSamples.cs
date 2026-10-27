using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCalibrationSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calibration_samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    TotalDurationMs = table.Column<int>(type: "integer", nullable: false),
                    AverageCharsPerSecond = table.Column<double>(type: "double precision", nullable: false),
                    CorrectionCount = table.Column<int>(type: "integer", nullable: false),
                    KeystrokeTimingsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calibration_samples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_calibration_samples_SessionId_FieldName",
                table: "calibration_samples",
                columns: new[] { "SessionId", "FieldName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calibration_samples");
        }
    }
}
