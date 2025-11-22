using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullClienteIdInFactura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CuentasPorCobrar_Clientes_ClienteId",
                table: "CuentasPorCobrar");

            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Clientes_ClienteId",
                table: "Facturas");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClienteId",
                table: "Facturas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClienteId",
                table: "CuentasPorCobrar",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_CuentasPorCobrar_Clientes_ClienteId",
                table: "CuentasPorCobrar",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Clientes_ClienteId",
                table: "Facturas",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CuentasPorCobrar_Clientes_ClienteId",
                table: "CuentasPorCobrar");

            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Clientes_ClienteId",
                table: "Facturas");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClienteId",
                table: "Facturas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClienteId",
                table: "CuentasPorCobrar",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CuentasPorCobrar_Clientes_ClienteId",
                table: "CuentasPorCobrar",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Clientes_ClienteId",
                table: "Facturas",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
