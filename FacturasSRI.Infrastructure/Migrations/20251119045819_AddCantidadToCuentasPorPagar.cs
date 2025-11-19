using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCantidadToCuentasPorPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("0bcd16a0-ef2f-463c-8aba-a3dfb5bcf471"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("1023960d-ec62-4ff9-8009-e42f494df014"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("24981466-8fdc-4f65-a63e-c699b6f75da6"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("9fad1530-eff0-4b60-bd2d-ebd5cecf1951"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("a1cafa86-4b8f-491d-a9d1-8239bab71a45"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("c2cbcaab-3dfb-4ce6-88f8-4aa60e55f9f7"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("d60ae813-4fc4-427d-90e3-7bcd38404739"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("ffa5172c-2633-4b2f-8f8c-e609954306f5"));

            migrationBuilder.AddColumn<int>(
                name: "Cantidad",
                table: "CuentasPorPagar",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cantidad",
                table: "CuentasPorPagar");

            migrationBuilder.InsertData(
                table: "Categorias",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { new Guid("0bcd16a0-ef2f-463c-8aba-a3dfb5bcf471"), "Componentes de PC" },
                    { new Guid("1023960d-ec62-4ff9-8009-e42f494df014"), "Impresión y Suministros" },
                    { new Guid("24981466-8fdc-4f65-a63e-c699b6f75da6"), "Redes y Conectividad" },
                    { new Guid("9fad1530-eff0-4b60-bd2d-ebd5cecf1951"), "Computadores y Laptops" },
                    { new Guid("a1cafa86-4b8f-491d-a9d1-8239bab71a45"), "Servidores y Enterprise" },
                    { new Guid("c2cbcaab-3dfb-4ce6-88f8-4aa60e55f9f7"), "Accesorios y Cables" },
                    { new Guid("d60ae813-4fc4-427d-90e3-7bcd38404739"), "Software y Licencias" },
                    { new Guid("ffa5172c-2633-4b2f-8f8c-e609954306f5"), "Periféricos" }
                });
        }
    }
}
