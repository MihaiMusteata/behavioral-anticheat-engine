using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAntiCaptureModeAndPhotosensitivitySafeMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PhotosensitivitySafeModeRequested",
                table: "exam_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AntiCaptureMode",
                table: "exam_schedules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "none");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotosensitivitySafeModeRequested",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "AntiCaptureMode",
                table: "exam_schedules");
        }
    }
}
