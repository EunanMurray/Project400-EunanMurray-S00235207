using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project400API.Migrations
{
    /// <inheritdoc />
    public partial class AddTailgateAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TailgateAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CameraDeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PeopleDetected = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AnalysisJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TailgateAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TailgateAlerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TailgateAlerts_CreatedAt",
                table: "TailgateAlerts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TailgateAlerts_DeviceId",
                table: "TailgateAlerts",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_TailgateAlerts_Status",
                table: "TailgateAlerts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TailgateAlerts_UserId",
                table: "TailgateAlerts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TailgateAlerts");
        }
    }
}
