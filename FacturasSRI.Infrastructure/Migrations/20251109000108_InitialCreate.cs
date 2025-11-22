using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FacturasSRI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoIdentificacion = table.Column<int>(type: "integer", nullable: false),
                    NumeroIdentificacion = table.Column<string>(type: "text", nullable: false),
                    RazonSocial = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Direccion = table.Column<string>(type: "text", nullable: false),
                    Telefono = table.Column<string>(type: "text", nullable: false),
                    EstaActivo = table.Column<bool>(type: "boolean", nullable: false),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Impuestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    CodigoSRI = table.Column<string>(type: "text", nullable: false),
                    Porcentaje = table.Column<decimal>(type: "numeric", nullable: false),
                    EstaActivo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Impuestos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoPrincipal = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    PrecioVentaUnitario = table.Column<decimal>(type: "numeric", nullable: false),
                    ManejaInventario = table.Column<bool>(type: "boolean", nullable: false),
                    ManejaLotes = table.Column<bool>(type: "boolean", nullable: false),
                    StockTotal = table.Column<int>(type: "integer", nullable: false),
                    EstaActivo = table.Column<bool>(type: "boolean", nullable: false),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    EstaActivo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Secuenciales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Establecimiento = table.Column<string>(type: "text", nullable: false),
                    PuntoEmision = table.Column<string>(type: "text", nullable: false),
                    UltimoSecuencialFactura = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Secuenciales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimerNombre = table.Column<string>(type: "text", nullable: false),
                    SegundoNombre = table.Column<string>(type: "text", nullable: true),
                    PrimerApellido = table.Column<string>(type: "text", nullable: false),
                    SegundoApellido = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    EstaActivo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Facturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NumeroFactura = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubtotalSinImpuestos = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalDescuento = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalIVA = table.Column<decimal>(type: "numeric", nullable: false),
                    Total = table.Column<decimal>(type: "numeric", nullable: false),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facturas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CantidadComprada = table.Column<int>(type: "integer", nullable: false),
                    CantidadDisponible = table.Column<int>(type: "integer", nullable: false),
                    PrecioCompraUnitario = table.Column<decimal>(type: "numeric", nullable: false),
                    FechaCompra = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCaducidad = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lotes_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductoImpuestos",
                columns: table => new
                {
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImpuestoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoImpuestos", x => new { x.ProductoId, x.ImpuestoId });
                    table.ForeignKey(
                        name: "FK_ProductoImpuestos_Impuestos_ImpuestoId",
                        column: x => x.ImpuestoId,
                        principalTable: "Impuestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductoImpuestos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioRoles",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    RolId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioRoles", x => new { x.UsuarioId, x.RolId });
                    table.ForeignKey(
                        name: "FK_UsuarioRoles_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioRoles_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CuentasPorCobrar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MontoTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    SaldoPendiente = table.Column<decimal>(type: "numeric", nullable: false),
                    Pagada = table.Column<bool>(type: "boolean", nullable: false),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasPorCobrar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuentasPorCobrar_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CuentasPorCobrar_Facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacturaDetalles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    PrecioVentaUnitario = table.Column<decimal>(type: "numeric", nullable: false),
                    Descuento = table.Column<decimal>(type: "numeric", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    ValorIVA = table.Column<decimal>(type: "numeric", nullable: false),
                    CostoTotal = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturaDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturaDetalles_Facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacturaDetalles_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacturasSRI",
                columns: table => new
                {
                    FacturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    XmlGenerado = table.Column<string>(type: "text", nullable: true),
                    XmlFirmado = table.Column<string>(type: "text", nullable: true),
                    ClaveAcceso = table.Column<string>(type: "text", nullable: true),
                    NumeroAutorizacion = table.Column<string>(type: "text", nullable: true),
                    FechaAutorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespuestaSRI = table.Column<string>(type: "text", nullable: true),
                    RideUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasSRI", x => x.FacturaId);
                    table.ForeignKey(
                        name: "FK_FacturasSRI_Facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotasDeCredito",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroNotaCredito = table.Column<string>(type: "text", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    RazonModificacion = table.Column<string>(type: "text", nullable: false),
                    SubtotalSinImpuestos = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalIVA = table.Column<decimal>(type: "numeric", nullable: false),
                    Total = table.Column<decimal>(type: "numeric", nullable: false),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasDeCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasDeCredito_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotasDeCredito_Facturas_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Facturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AjustesInventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    CantidadAjustada = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioIdAutoriza = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AjustesInventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AjustesInventario_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CuentasPorPagar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Proveedor = table.Column<string>(type: "text", nullable: false),
                    NumeroFactura = table.Column<string>(type: "text", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MontoTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    SaldoPendiente = table.Column<decimal>(type: "numeric", nullable: false),
                    Pagada = table.Column<bool>(type: "boolean", nullable: false),
                    UsuarioIdCreador = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasPorPagar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuentasPorPagar_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FacturaDetalleConsumoLotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacturaDetalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CantidadConsumida = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturaDetalleConsumoLotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturaDetalleConsumoLotes_FacturaDetalles_FacturaDetalleId",
                        column: x => x.FacturaDetalleId,
                        principalTable: "FacturaDetalles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacturaDetalleConsumoLotes_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotaDeCreditoDetalles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotaDeCreditoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    PrecioVentaUnitario = table.Column<decimal>(type: "numeric", nullable: false),
                    DescuentoAplicado = table.Column<decimal>(type: "numeric", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    ValorIVA = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotaDeCreditoDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotaDeCreditoDetalles_NotasDeCredito_NotaDeCreditoId",
                        column: x => x.NotaDeCreditoId,
                        principalTable: "NotasDeCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotaDeCreditoDetalles_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotasDeCreditoSRI",
                columns: table => new
                {
                    NotaDeCreditoId = table.Column<Guid>(type: "uuid", nullable: false),
                    XmlGenerado = table.Column<string>(type: "text", nullable: true),
                    XmlFirmado = table.Column<string>(type: "text", nullable: true),
                    ClaveAcceso = table.Column<string>(type: "text", nullable: true),
                    NumeroAutorizacion = table.Column<string>(type: "text", nullable: true),
                    FechaAutorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespuestaSRI = table.Column<string>(type: "text", nullable: true),
                    RideUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasDeCreditoSRI", x => x.NotaDeCreditoId);
                    table.ForeignKey(
                        name: "FK_NotasDeCreditoSRI_NotasDeCredito_NotaDeCreditoId",
                        column: x => x.NotaDeCreditoId,
                        principalTable: "NotasDeCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Descripcion", "EstaActivo", "Nombre" },
                values: new object[,]
                {
                    { new Guid("d3b1b4a9-2f7b-4e6a-9f6b-1c2c3d4e5f6a"), "Acceso total al sistema.", true, "Administrador" },
                    { new Guid("e2a87c46-e5b3-4f9e-8c6e-1f2a3b4c5d6e"), "Puede gestionar clientes y facturas.", true, "Vendedor" },
                    { new Guid("f5b8c9d0-1a2b-3c4d-5e6f-7a8b9c0d1e2f"), "Puede gestionar productos, compras e inventario.", true, "Bodeguero" }
                });

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "Email", "EstaActivo", "PasswordHash", "PrimerApellido", "PrimerNombre", "SegundoApellido", "SegundoNombre" },
                values: new object[] { new Guid("a9b1b4d3-3f7b-4e6a-9f6b-1c2c3d4e5f6b"), "admin@facturassri.com", true, "$2a$11$KnYr45JSbCoMg4Jtkg0GXegC7SegKYTidLxFYYljNwtLH0l024qLG", "Aether", "Admin", null, null });

            migrationBuilder.InsertData(
                table: "UsuarioRoles",
                columns: new[] { "RolId", "UsuarioId" },
                values: new object[] { new Guid("d3b1b4a9-2f7b-4e6a-9f6b-1c2c3d4e5f6a"), new Guid("a9b1b4d3-3f7b-4e6a-9f6b-1c2c3d4e5f6b") });

            migrationBuilder.CreateIndex(
                name: "IX_AjustesInventario_LoteId",
                table: "AjustesInventario",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorCobrar_ClienteId",
                table: "CuentasPorCobrar",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorCobrar_FacturaId",
                table: "CuentasPorCobrar",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasPorPagar_LoteId",
                table: "CuentasPorPagar",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaDetalleConsumoLotes_FacturaDetalleId",
                table: "FacturaDetalleConsumoLotes",
                column: "FacturaDetalleId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaDetalleConsumoLotes_LoteId",
                table: "FacturaDetalleConsumoLotes",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaDetalles_FacturaId",
                table: "FacturaDetalles",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaDetalles_ProductoId",
                table: "FacturaDetalles",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_ClienteId",
                table: "Facturas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_ProductoId",
                table: "Lotes",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_NotaDeCreditoDetalles_NotaDeCreditoId",
                table: "NotaDeCreditoDetalles",
                column: "NotaDeCreditoId");

            migrationBuilder.CreateIndex(
                name: "IX_NotaDeCreditoDetalles_ProductoId",
                table: "NotaDeCreditoDetalles",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasDeCredito_ClienteId",
                table: "NotasDeCredito",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasDeCredito_FacturaId",
                table: "NotasDeCredito",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoImpuestos_ImpuestoId",
                table: "ProductoImpuestos",
                column: "ImpuestoId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRoles_RolId",
                table: "UsuarioRoles",
                column: "RolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AjustesInventario");

            migrationBuilder.DropTable(
                name: "CuentasPorCobrar");

            migrationBuilder.DropTable(
                name: "CuentasPorPagar");

            migrationBuilder.DropTable(
                name: "FacturaDetalleConsumoLotes");

            migrationBuilder.DropTable(
                name: "FacturasSRI");

            migrationBuilder.DropTable(
                name: "NotaDeCreditoDetalles");

            migrationBuilder.DropTable(
                name: "NotasDeCreditoSRI");

            migrationBuilder.DropTable(
                name: "ProductoImpuestos");

            migrationBuilder.DropTable(
                name: "Secuenciales");

            migrationBuilder.DropTable(
                name: "UsuarioRoles");

            migrationBuilder.DropTable(
                name: "FacturaDetalles");

            migrationBuilder.DropTable(
                name: "Lotes");

            migrationBuilder.DropTable(
                name: "NotasDeCredito");

            migrationBuilder.DropTable(
                name: "Impuestos");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Facturas");

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
