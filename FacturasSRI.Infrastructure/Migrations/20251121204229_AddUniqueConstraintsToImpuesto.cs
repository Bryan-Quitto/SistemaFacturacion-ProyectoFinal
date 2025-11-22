using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintsToImpuesto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Impuestos_CodigoSRI",
                table: "Impuestos",
                column: "CodigoSRI",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Impuestos_Nombre",
                table: "Impuestos",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Impuestos_CodigoSRI",
                table: "Impuestos");

            migrationBuilder.DropIndex(
                name: "IX_Impuestos_Nombre",
                table: "Impuestos");
        }
    }
}
