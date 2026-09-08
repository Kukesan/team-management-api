using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReportUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_UserId_ProjectId_WeekStartDate",
                table: "Reports");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_UserId_ProjectId_WeekStartDate",
                table: "Reports",
                columns: new[] { "UserId", "ProjectId", "WeekStartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_UserId_ProjectId_WeekStartDate",
                table: "Reports");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_UserId_ProjectId_WeekStartDate",
                table: "Reports",
                columns: new[] { "UserId", "ProjectId", "WeekStartDate" },
                unique: true);
        }
    }
}
