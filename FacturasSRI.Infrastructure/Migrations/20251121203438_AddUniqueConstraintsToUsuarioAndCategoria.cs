using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintsToUsuarioAndCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("127dea85-8977-482a-ba61-a1e6226705c9"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("37d86aff-3a26-4f14-98c3-63f2488796d8"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("fc127df0-c9ec-4203-8016-5880b4cf4ee1"));

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            /*
            migrationBuilder.CreateIndex(
                name: "IX_Categorias_Nombre",
                table: "Categorias",
                column: "Nombre",
                unique: true);
            */
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios");

            /*
            migrationBuilder.DropIndex(
                name: "IX_Categorias_Nombre",
                table: "Categorias");
            */

            migrationBuilder.InsertData(
                table: "Categorias",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { new Guid("127dea85-8977-482a-ba61-a1e6226705c9"), "Servicios Profesionales" },
                    { new Guid("37d86aff-3a26-4f14-98c3-63f2488796d8"), "Soporte y Mantenimiento" },
                    { new Guid("fc127df0-c9ec-4203-8016-5880b4cf4ee1"), "Servicios Técnicos" }
                });
        }
    }
}
