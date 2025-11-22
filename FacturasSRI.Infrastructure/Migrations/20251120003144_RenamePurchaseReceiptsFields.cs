using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamePurchaseReceiptsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ComprobantePath",
                table: "CuentasPorPagar",
                newName: "FacturaCompraPath");

            migrationBuilder.AddColumn<string>(
                name: "ComprobantePagoPath",
                table: "CuentasPorPagar",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComprobantePagoPath",
                table: "CuentasPorPagar");

            migrationBuilder.RenameColumn(
                name: "FacturaCompraPath",
                table: "CuentasPorPagar",
                newName: "ComprobantePath");
        }
    }
}
