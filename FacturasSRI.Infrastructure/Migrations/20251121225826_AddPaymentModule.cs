using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasCredito",
                table: "Facturas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FormaDePago",
                table: "Facturas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Cobros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCobro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric", nullable: false),
                    MetodoDePago = table.Column<string>(type: "text", nullable: false),
                    Referencia = table.Column<string>(type: "text", nullable: true),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cobros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cobros_Facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Cobros_Usuarios_UsuarioIdCreador",
                        column: x => x.UsuarioIdCreador,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_FacturaId",
                table: "Cobros",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_UsuarioIdCreador",
                table: "Cobros",
                column: "UsuarioIdCreador");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cobros");

            migrationBuilder.DropColumn(
                name: "DiasCredito",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "FormaDePago",
                table: "Facturas");
        }
    }
}
