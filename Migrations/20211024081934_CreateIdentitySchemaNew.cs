using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SmartCubeMobileV2026.Migrations
{
    public partial class CreateIdentitySchemaNew : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Administrator",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ClearPassword",
                table: "AspNetUsers",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Expiration1",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "Expiration2",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "Expiration3",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLogOnTime",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "MultipleMeter",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Subscriber",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Trace",
                table: "AspNetUsers",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Administrator",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ClearPassword",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Expiration1",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Expiration2",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Expiration3",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLogOnTime",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MultipleMeter",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Subscriber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Trace",
                table: "AspNetUsers");
        }
    }
}
