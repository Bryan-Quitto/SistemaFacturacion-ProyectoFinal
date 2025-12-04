using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentificadoresToCuentaPorPagar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UltimoSecuencialCompra",
                table: "Secuenciales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NumeroCompraInterno",
                table: "CuentasPorPagar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NumeroFacturaProveedor",
                table: "CuentasPorPagar",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UltimoSecuencialCompra",
                table: "Secuenciales");

            migrationBuilder.DropColumn(
                name: "NumeroCompraInterno",
                table: "CuentasPorPagar");

            migrationBuilder.DropColumn(
                name: "NumeroFacturaProveedor",
                table: "CuentasPorPagar");
        }
    }
}
