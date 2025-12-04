using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioRelationToCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorPagar_UsuarioIdCreador",
                table: "CuentasPorPagar",
                column: "UsuarioIdCreador");

            migrationBuilder.AddForeignKey(
                name: "FK_CuentasPorPagar_Usuarios_UsuarioIdCreador",
                table: "CuentasPorPagar",
                column: "UsuarioIdCreador",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CuentasPorPagar_Usuarios_UsuarioIdCreador",
                table: "CuentasPorPagar");

            migrationBuilder.DropIndex(
                name: "IX_CuentasPorPagar_UsuarioIdCreador",
                table: "CuentasPorPagar");
        }
    }
}
