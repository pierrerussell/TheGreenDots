using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectCallisto.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class PresenceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PresenceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Availability = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Activity = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresenceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresenceHistories_TenantMembers_TenantMemberId",
                        column: x => x.TenantMemberId,
                        principalTable: "TenantMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PresenceHistories_TenantMemberId_RecordedAt",
                table: "PresenceHistories",
                columns: new[] { "TenantMemberId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PresenceHistories");
        }
    }
}
