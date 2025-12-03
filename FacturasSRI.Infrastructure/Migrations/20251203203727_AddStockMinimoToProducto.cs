using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMinimoToProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StockMinimo",
                table: "Productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockMinimo",
                table: "Productos");
        }
    }
}
