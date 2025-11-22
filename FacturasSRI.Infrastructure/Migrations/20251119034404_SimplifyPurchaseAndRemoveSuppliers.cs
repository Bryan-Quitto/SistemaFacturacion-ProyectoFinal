using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FacturasSRI.Infrastructure.Migrations
{
    public partial class SimplifyPurchaseAndRemoveSuppliers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Proveedores_ProveedorId",
                table: "Lotes");

            migrationBuilder.DropTable(
                name: "Proveedores");

            migrationBuilder.DropIndex(
                name: "IX_Lotes_ProveedorId",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "ProveedorId",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "NumeroFactura",
                table: "CuentasPorPagar");

            migrationBuilder.RenameColumn(
                name: "Proveedor",
                table: "CuentasPorPagar",
                newName: "NombreProveedor");

            migrationBuilder.AddColumn<string>(
                name: "ComprobantePath",
                table: "CuentasPorPagar",
                type: "text",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Categorias",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { new Guid("0771c970-05a4-4224-83db-c4d3a7d00513"), "Impresión y Suministros" },
                    { new Guid("0f1e46b4-5d04-4a0f-b723-1cd5c60d3317"), "Computadores y Laptops" },
                    { new Guid("5a91a6f6-8ce9-44e5-b0d2-69dea6b48551"), "Software y Licencias" },
                    { new Guid("84775e6f-7a08-4b7b-8696-e9c317d28d72"), "Accesorios y Cables" },
                    { new Guid("96177625-2856-46b1-8ae2-b5546808218d"), "Componentes de PC" },
                    { new Guid("a9e8fa2f-c82f-4889-8833-c46104c6606e"), "Servidores y Enterprise" },
                    { new Guid("b796f9ef-23d9-4d01-8ed8-95090516b802"), "Periféricos" },
                    { new Guid("f7b49ca0-3525-4e95-885c-ab7812465878"), "Redes y Conectividad" }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("0771c970-05a4-4224-83db-c4d3a7d00513"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("0f1e46b4-5d04-4a0f-b723-1cd5c60d3317"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("5a91a6f6-8ce9-44e5-b0d2-69dea6b48551"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("84775e6f-7a08-4b7b-8696-e9c317d28d72"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("96177625-2856-46b1-8ae2-b5546808218d"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("a9e8fa2f-c82f-4889-8833-c46104c6606e"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("b796f9ef-23d9-4d01-8ed8-95090516b802"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("f7b49ca0-3525-4e95-885c-ab7812465878"));

            migrationBuilder.DropColumn(
                name: "ComprobantePath",
                table: "CuentasPorPagar");

            migrationBuilder.RenameColumn(
                name: "NombreProveedor",
                table: "CuentasPorPagar",
                newName: "Proveedor");

            migrationBuilder.AddColumn<Guid>(
                name: "ProveedorId",
                table: "Lotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroFactura",
                table: "CuentasPorPagar",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Proveedores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Direccion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EstaActivo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RUC = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RazonSocial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_ProveedorId",
                table: "Lotes",
                column: "ProveedorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Proveedores_ProveedorId",
                table: "Lotes",
                column: "ProveedorId",
                principalTable: "Proveedores",
                principalColumn: "Id");
        }
    }
}