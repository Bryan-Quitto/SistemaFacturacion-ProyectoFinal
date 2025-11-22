using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProveedorSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Proveedor_ProveedorId",
                table: "Lotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Proveedor",
                table: "Proveedor");

            migrationBuilder.RenameTable(
                name: "Proveedor",
                newName: "Proveedores");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Proveedores",
                table: "Proveedores",
                column: "Id");

            migrationBuilder.InsertData(
                table: "Proveedores",
                columns: new[] { "Id", "Direccion", "Email", "EstaActivo", "FechaCreacion", "RUC", "RazonSocial", "Telefono", "UsuarioIdCreador" },
                values: new object[] { new Guid("c1d2e3f4-5a6b-7c8d-9e0f-1a2b3c4d5e6f"), "N/A", "proveedor.general@example.com", true, new DateTime(2025, 11, 9, 23, 37, 14, 72, DateTimeKind.Utc).AddTicks(5685), "9999999999001", "Proveedor General", "N/A", new Guid("a9b1b4d3-3f7b-4e6a-9f6b-1c2c3d4e5f6b") });

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Proveedores_ProveedorId",
                table: "Lotes",
                column: "ProveedorId",
                principalTable: "Proveedores",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Proveedores_ProveedorId",
                table: "Lotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Proveedores",
                table: "Proveedores");

            migrationBuilder.DeleteData(
                table: "Proveedores",
                keyColumn: "Id",
                keyValue: new Guid("c1d2e3f4-5a6b-7c8d-9e0f-1a2b3c4d5e6f"));

            migrationBuilder.RenameTable(
                name: "Proveedores",
                newName: "Proveedor");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Proveedor",
                table: "Proveedor",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Proveedor_ProveedorId",
                table: "Lotes",
                column: "ProveedorId",
                principalTable: "Proveedor",
                principalColumn: "Id");
        }
    }
}
