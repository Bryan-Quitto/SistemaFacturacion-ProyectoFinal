using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCuentasPorPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pagada",
                table: "CuentasPorPagar");

            migrationBuilder.DropColumn(
                name: "SaldoPendiente",
                table: "CuentasPorPagar");

            migrationBuilder.AddColumn<int>(
                name: "Estado",
                table: "CuentasPorPagar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaPago",
                table: "CuentasPorPagar",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Estado",
                table: "CuentasPorPagar");

            migrationBuilder.DropColumn(
                name: "FechaPago",
                table: "CuentasPorPagar");

            migrationBuilder.AddColumn<bool>(
                name: "Pagada",
                table: "CuentasPorPagar",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoPendiente",
                table: "CuentasPorPagar",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
