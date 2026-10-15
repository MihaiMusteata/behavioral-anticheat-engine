using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BehavioralAnticheatEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePhotosensitivitySafeModeRequested : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotosensitivitySafeModeRequested",
                table: "exam_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PhotosensitivitySafeModeRequested",
                table: "exam_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
