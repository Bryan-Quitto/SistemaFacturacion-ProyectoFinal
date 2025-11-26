using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrecioCompraPromedioPonderadoToProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecioCompraPromedioPonderado",
                table: "Productos",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecioCompraPromedioPonderado",
                table: "Productos");
        }
    }
}
