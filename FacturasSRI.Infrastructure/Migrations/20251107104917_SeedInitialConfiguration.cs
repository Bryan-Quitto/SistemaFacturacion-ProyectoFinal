using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    public partial class SeedInitialConfiguration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var empresaId = Guid.NewGuid();
            var establecimientoId = Guid.NewGuid();
            var puntoEmisionId = Guid.NewGuid();

            migrationBuilder.InsertData(
                table: "Empresas",
                columns: new[] { "Id", "Ruc", "RazonSocial", "NombreComercial", "DireccionMatriz", "ContribuyenteEspecial", "ObligadoContabilidad", "EstaActiva" },
                values: new object[] { empresaId, "9999999999999", "MI EMPRESA S.A.", "MI EMPRESA", "AV. SIEMPRE VIVA 123", "000", false, true });

            migrationBuilder.InsertData(
                table: "Establecimientos",
                columns: new[] { "Id", "EmpresaId", "Codigo", "Nombre", "Direccion", "EstaActivo" },
                values: new object[] { establecimientoId, empresaId, "001", "MATRIZ", "AV. SIEMPRE VIVA 123", true });

            migrationBuilder.InsertData(
                table: "PuntosEmision",
                columns: new[] { "Id", "EstablecimientoId", "Codigo", "SecuencialFactura", "SecuencialNotaCredito", "EstaActivo" },
                values: new object[] { puntoEmisionId, establecimientoId, "001", 1, 1, true });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Opcional: código para borrar los datos si se revierte la migración
        }
    }
}