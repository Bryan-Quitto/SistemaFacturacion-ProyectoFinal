using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FacturasSRI.Infrastructure.Migrations
{
    public partial class AddProductoToCuentasPorPagar : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductoId",
                table: "CuentasPorPagar",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorPagar_ProductoId",
                table: "CuentasPorPagar",
                column: "ProductoId");

            migrationBuilder.AddForeignKey(
                name: "FK_CuentasPorPagar_Productos_ProductoId",
                table: "CuentasPorPagar",
                column: "ProductoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CuentasPorPagar_Productos_ProductoId",
                table: "CuentasPorPagar");

            migrationBuilder.DropIndex(
                name: "IX_CuentasPorPagar_ProductoId",
                table: "CuentasPorPagar");

            migrationBuilder.DropColumn(
                name: "ProductoId",
                table: "CuentasPorPagar");
        }
    }
}