using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Reservations.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotStartToReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Lanes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Lanes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Lanes",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Lanes",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Lanes",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Lanes",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlotStart",
                table: "Reservations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlotStart",
                table: "Reservations");

            migrationBuilder.InsertData(
                table: "Lanes",
                columns: new[] { "Id", "Capacity", "Name" },
                values: new object[,]
                {
                    { 1, 1, "Tor 1" },
                    { 2, 1, "Tor 2" },
                    { 3, 1, "Tor 3" },
                    { 4, 1, "Tor 4" },
                    { 5, 1, "Tor 5" },
                    { 6, 1, "Tor 6" }
                });
        }
    }
}
