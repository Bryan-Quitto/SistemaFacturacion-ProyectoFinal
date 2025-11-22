using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsToEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                table: "Productos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "Impuestos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                table: "Impuestos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                table: "Clientes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: new Guid("a9b1b4d3-3f7b-4e6a-9f6b-1c2c3d4e5f6b"),
                columns: new[] { "FechaCreacion", "FechaModificacion" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "Impuestos");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                table: "Impuestos");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                table: "Clientes");
        }
    }
}
