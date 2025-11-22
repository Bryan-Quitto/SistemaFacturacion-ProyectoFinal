using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProveedorAndLinkToLoteNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProveedorId",
                table: "Lotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Proveedor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RUC = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RazonSocial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Direccion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EstaActivo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proveedor", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_ProveedorId",
                table: "Lotes",
                column: "ProveedorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lotes_Proveedor_ProveedorId",
                table: "Lotes",
                column: "ProveedorId",
                principalTable: "Proveedor",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lotes_Proveedor_ProveedorId",
                table: "Lotes");

            migrationBuilder.DropTable(
                name: "Proveedor");

            migrationBuilder.DropIndex(
                name: "IX_Lotes_ProveedorId",
                table: "Lotes");

            migrationBuilder.DropColumn(
                name: "ProveedorId",
                table: "Lotes");
        }
    }
}
