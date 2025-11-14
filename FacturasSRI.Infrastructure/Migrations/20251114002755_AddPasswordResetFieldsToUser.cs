using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetFieldsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                table: "Usuarios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiry",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Proveedores",
                keyColumn: "Id",
                keyValue: new Guid("c1d2e3f4-5a6b-7c8d-9e0f-1a2b3c4d5e6f"),
                column: "FechaCreacion",
                value: new DateTime(2025, 11, 14, 0, 27, 55, 181, DateTimeKind.Utc).AddTicks(258));

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: new Guid("a9b1b4d3-3f7b-4e6a-9f6b-1c2c3d4e5f6b"),
                columns: new[] { "PasswordResetToken", "PasswordResetTokenExpiry" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiry",
                table: "Usuarios");

            migrationBuilder.UpdateData(
                table: "Proveedores",
                keyColumn: "Id",
                keyValue: new Guid("c1d2e3f4-5a6b-7c8d-9e0f-1a2b3c4d5e6f"),
                column: "FechaCreacion",
                value: new DateTime(2025, 11, 10, 3, 1, 15, 655, DateTimeKind.Utc).AddTicks(5382));
        }
    }
}
