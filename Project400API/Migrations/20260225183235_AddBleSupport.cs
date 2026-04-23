using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project400API.Migrations
{
    /// <inheritdoc />
    public partial class AddBleSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BleDeviceId",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BleTriggerTime",
                table: "UnlockRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BleTriggered",
                table: "UnlockRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BleDeviceId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BleTriggerTime",
                table: "UnlockRequests");

            migrationBuilder.DropColumn(
                name: "BleTriggered",
                table: "UnlockRequests");
        }
    }
}
