using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UltimaModificacionPor",
                table: "Clientes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioModificadorId",
                table: "Clientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Proveedores",
                keyColumn: "Id",
                keyValue: new Guid("c1d2e3f4-5a6b-7c8d-9e0f-1a2b3c4d5e6f"),
                column: "FechaCreacion",
                value: new DateTime(2025, 11, 10, 3, 1, 15, 655, DateTimeKind.Utc).AddTicks(5382));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UltimaModificacionPor",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "UsuarioModificadorId",
                table: "Clientes");

            migrationBuilder.UpdateData(
                table: "Proveedores",
                keyColumn: "Id",
                keyValue: new Guid("c1d2e3f4-5a6b-7c8d-9e0f-1a2b3c4d5e6f"),
                column: "FechaCreacion",
                value: new DateTime(2025, 11, 9, 23, 37, 14, 72, DateTimeKind.Utc).AddTicks(5685));
        }
    }
}
