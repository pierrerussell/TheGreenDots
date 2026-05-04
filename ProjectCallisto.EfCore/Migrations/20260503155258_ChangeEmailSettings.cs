using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectCallisto.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class ChangeEmailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailReportSettings_OrganisationId",
                table: "EmailReportSettings");

            migrationBuilder.CreateIndex(
                name: "IX_EmailReportSettings_OrganisationId",
                table: "EmailReportSettings",
                column: "OrganisationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailReportSettings_OrganisationId",
                table: "EmailReportSettings");

            migrationBuilder.CreateIndex(
                name: "IX_EmailReportSettings_OrganisationId",
                table: "EmailReportSettings",
                column: "OrganisationId",
                unique: true);
        }
    }
}
