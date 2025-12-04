using FacturasSRI.Application.Dtos.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using System.Globalization;

namespace FacturasSRI.Infrastructure.Services
{
    public class ReportPdfGeneratorService
    {
        private readonly IWebHostEnvironment _env;
        private readonly CultureInfo esEcCulture = new CultureInfo("es-EC");

        public ReportPdfGeneratorService(IWebHostEnvironment env)
        {
            _env = env;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // --- MÉTODOS DE GENERACIÓN ---

        public byte[] GenerateVentasPorPeriodoPdf(IEnumerable<VentasPorPeriodoDto> data, DateTime startDate, DateTime endDate)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Ventas por Período", startDate, endDate));
                    page.Content().Element(compose => ComposeContentVentas(compose, data));
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateVentasPorProductoPdf(IEnumerable<VentasPorProductoDto> data, DateTime startDate, DateTime endDate)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Ventas por Producto", startDate, endDate));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(60);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Código");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).Text("Fecha");
                                header.Cell().Element(HeaderCellStyle).Text("Vendedor");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Cantidad Vendida");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Precio Promedio");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total Vendido");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.CodigoProducto);
                                table.Cell().Element(DataCellStyle).Text(item.NombreProducto);
                                table.Cell().Element(DataCellStyle).Text(item.Fecha.ToString("dd/MM/yyyy"));
                                table.Cell().Element(DataCellStyle).Text(item.Vendedor);
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.CantidadVendida.ToString("N2"));
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.PrecioPromedio.ToString("C", esEcCulture));
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.TotalVendido.ToString("C", esEcCulture));
                            }
                            table.Cell().ColumnSpan(7).Element(TotalsRowStyle).Row(row =>
                            {
                                row.RelativeItem(3.1f).AlignRight().Text("Total:").Bold();
                                row.RelativeItem(1).AlignRight().Text(data.Sum(x => x.CantidadVendida).ToString("N2")).Bold();
                                row.RelativeItem(1);
                                row.RelativeItem(1).AlignRight().Text(data.Sum(x => x.TotalVendido).ToString("C", esEcCulture)).Bold();
                            });
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateActividadClientesPdf(IEnumerable<ClienteActividadDto> data, DateTime startDate, DateTime endDate)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Actividad de Clientes", startDate, endDate));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(2);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Cliente");
                                header.Cell().Element(HeaderCellStyle).Text("Identificación");
                                header.Cell().Element(HeaderCellStyle).Text("Última Compra");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Días Inactivo");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("# Compras");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total Comprado");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.NombreCliente);
                                table.Cell().Element(DataCellStyle).Text(item.Identificacion);
                                table.Cell().Element(DataCellStyle).Text(item.UltimaCompra?.ToString("dd/MM/yyyy") ?? "N/A");
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.DiasDesdeUltimaCompra == -1 ? "N/A" : item.DiasDesdeUltimaCompra.ToString());
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.NumeroDeCompras.ToString());
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.TotalComprado.ToString("C", esEcCulture));
                            }
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateCuentasPorCobrarPdf(IEnumerable<CuentasPorCobrarDto> data)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Cuentas por Cobrar"));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Cliente");
                                header.Cell().Element(HeaderCellStyle).Text("Factura #");
                                header.Cell().Element(HeaderCellStyle).Text("Vendedor");
                                header.Cell().Element(HeaderCellStyle).Text("Fecha Emisión");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Días Vencida");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Monto Factura");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Monto Pagado");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Saldo Pendiente");
                            });
                            foreach (var item in data.OrderByDescending(x => x.DiasVencida))
                            {
                                var backgroundColor = item.DiasVencida > 30 ? Colors.Red.Lighten4
                                                    : item.DiasVencida > 15 ? Colors.Yellow.Lighten4
                                                    : Colors.White;

                                table.Cell().Background(backgroundColor).Element(DataCellStyle).Text(item.NombreCliente);
                                table.Cell().Background(backgroundColor).Element(DataCellStyle).Text(item.NumeroFactura);
                                table.Cell().Background(backgroundColor).Element(DataCellStyle).Text(item.Vendedor);
                                table.Cell().Background(backgroundColor).Element(DataCellStyle).Text(item.FechaEmision.ToString("dd/MM/yyyy"));
                                table.Cell().Background(backgroundColor).Element(DataCellStyle).AlignRight().Text(item.DiasVencida.ToString());
                                table.Cell().Background(backgroundColor).Element(DataCellStyle).AlignRight().Text(item.MontoFactura.ToString("C", esEcCulture));
                                table.Cell().Background(backgroundColor).Element(DataCellStyle).AlignRight().Text(item.MontoPagado.ToString("C", esEcCulture));
                                table.Cell().Background(backgroundColor).Element(DataCellStyle).AlignRight().Text(item.SaldoPendiente.ToString("C", esEcCulture)).Bold();
                            }
                            table.Cell().ColumnSpan(8).Element(TotalsRowStyle).Row(row =>
                            {
                                row.RelativeItem(11.5f).AlignRight().Text("Total Pendiente:").Bold();
                                row.RelativeItem(2).AlignRight().Text(data.Sum(x => x.SaldoPendiente).ToString("C", esEcCulture)).Bold();
                            });
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateNotasDeCreditoPdf(IEnumerable<NotasDeCreditoReportDto> data, DateTime startDate, DateTime endDate)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Notas de Crédito Emitidas", startDate, endDate));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("# Nota de Crédito");
                                header.Cell().Element(HeaderCellStyle).Text("Fecha Emisión");
                                header.Cell().Element(HeaderCellStyle).Text("Cliente");
                                header.Cell().Element(HeaderCellStyle).Text("Factura Afectada");
                                header.Cell().Element(HeaderCellStyle).Text("Motivo");
                                header.Cell().Element(HeaderCellStyle).Text("Vendedor");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Valor");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.NumeroNotaCredito);
                                table.Cell().Element(DataCellStyle).Text(item.FechaEmision.ToString("dd/MM/yyyy"));
                                table.Cell().Element(DataCellStyle).Text(item.NombreCliente);
                                table.Cell().Element(DataCellStyle).Text(item.FacturaModificada);
                                table.Cell().Element(DataCellStyle).Text(item.Motivo);
                                table.Cell().Element(DataCellStyle).Text(item.Vendedor);
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.ValorTotal.ToString("C", esEcCulture));
                            }
                            table.Cell().ColumnSpan(7).Element(TotalsRowStyle).Row(row =>
                            {
                                row.RelativeItem(13.5f).AlignRight().Text("Total Devuelto:").Bold();
                                row.RelativeItem(2).AlignRight().Text(data.Sum(x => x.ValorTotal).ToString("C", esEcCulture)).Bold();
                            });
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateStockActualPdf(IEnumerable<StockActualDto> data, bool hiddenZeros)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    var title = hiddenZeros ? "Reporte de Stock Actual (Solo productos con stock)" : "Reporte de Stock Actual (Completo)";
                    page.Header().Element(compose => ComposeHeader(compose, title));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn();
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Código");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).Text("Categoría");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Stock");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Costo Prom.");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Valor Inv.");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.CodigoPrincipal);
                                table.Cell().Element(DataCellStyle).Text(item.NombreProducto);
                                table.Cell().Element(DataCellStyle).Text(item.Categoria);
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.StockTotal.ToString());
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.PrecioCompraPromedioPonderado.ToString("C", esEcCulture));
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.ValorInventario.ToString("C", esEcCulture));
                            }
                            table.Cell().ColumnSpan(6).Element(TotalsRowStyle).Row(row =>
                            {
                                row.RelativeItem(9f).AlignRight().Text("Valor Total del Inventario:").Bold();
                                row.RelativeItem(1.5f).AlignRight().Text(data.Sum(x => x.ValorInventario).ToString("C", esEcCulture)).Bold();
                            });
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateMovimientosInventarioPdf(IEnumerable<MovimientoInventarioDto> data, DateTime startDate, DateTime endDate)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Kardex de Movimientos", startDate, endDate));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f); // Fecha
                                columns.RelativeColumn(1.5f); // Resp
                                columns.RelativeColumn(3);    // Prod
                                columns.RelativeColumn(1.5f); // Tipo
                                columns.RelativeColumn(3);    // Doc
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Fecha");
                                header.Cell().Element(HeaderCellStyle).Text("Resp.");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).Text("Tipo");
                                header.Cell().Element(HeaderCellStyle).Text("Documento");
                                // CORRECCIÓN AQUÍ: Aplicamos el color al texto, no a la celda completa de forma incorrecta
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Entrada").FontColor(Colors.Green.Darken2);
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Salida").FontColor(Colors.Red.Darken2);
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.Fecha.ToString("dd/MM/yyyy HH:mm"));
                                table.Cell().Element(DataCellStyle).Text(item.UsuarioResponsable);
                                table.Cell().Element(DataCellStyle).Text(item.ProductoNombre);
                                table.Cell().Element(DataCellStyle).Text(item.TipoMovimiento);
                                table.Cell().Element(DataCellStyle).Text(item.DocumentoReferencia);

                                // CORRECCIÓN AQUÍ TAMBIÉN
                                table.Cell().Element(DataCellStyle).AlignRight()
                                    .Text(item.Entrada > 0 ? item.Entrada.ToString() : "-").FontColor(Colors.Green.Darken2);

                                table.Cell().Element(DataCellStyle).AlignRight()
                                    .Text(item.Salida > 0 ? item.Salida.ToString() : "-").FontColor(Colors.Red.Darken2);
                            }
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateComprasPorPeriodoPdf(IEnumerable<ComprasPorPeriodoDto> data, DateTime startDate, DateTime endDate)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte Detallado de Compras", startDate, endDate));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.3f); // Fecha
                                columns.RelativeColumn(1.5f); // Resp 
                                columns.RelativeColumn(2.2f); // Prov
                                columns.RelativeColumn(2.0f); // Doc
                                columns.RelativeColumn(3);    // Prod
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Fecha");
                                header.Cell().Element(HeaderCellStyle).Text("Resp.");
                                header.Cell().Element(HeaderCellStyle).Text("Proveedor");
                                header.Cell().Element(HeaderCellStyle).Text("Documento");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Cant.");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Costo Unit.");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.Fecha.ToString("dd/MM/yyyy"));
                                table.Cell().Element(DataCellStyle).Text(item.UsuarioResponsable);
                                table.Cell().Element(DataCellStyle).Text(item.NombreProveedor);

                                table.Cell().Element(DataCellStyle).Column(col =>
                                {
                                    if (!string.IsNullOrEmpty(item.NumeroFacturaProveedor))
                                        col.Item().Text($"Prov: {item.NumeroFacturaProveedor}").FontSize(8);

                                    col.Item().Text($"Int: #{item.NumeroCompraInterno}").FontSize(8).Italic();
                                });

                                table.Cell().Element(DataCellStyle).Text(item.ProductoNombre);
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.CantidadComprada.ToString("N0"));
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.CostoUnitario.ToString("C", esEcCulture));
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.CostoTotal.ToString("C", esEcCulture));
                            }
                            table.Cell().ColumnSpan(7).Element(TotalsRowStyle).Row(row =>
                            {
                                row.RelativeItem(10.5f).AlignRight().Text("Totales:").Bold();
                                row.RelativeItem(1).AlignRight().Text(data.Sum(x => x.CantidadComprada).ToString("N0")).Bold();
                                row.RelativeItem(1.5f);
                                row.RelativeItem(1.5f).AlignRight().Text(data.Sum(x => x.CostoTotal).ToString("C", esEcCulture)).Bold();
                            });
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateProductosBajoStockMinimoPdf(IEnumerable<ProductoStockMinimoDto> data)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reposición de Inventario (Bajo Stock Mínimo)"));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Código");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Actual");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Mínimo");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Falta");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Costo Unit.");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Inversión Req.");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.CodigoPrincipal);
                                table.Cell().Element(DataCellStyle).Text(item.NombreProducto);
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.StockActual.ToString());
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.StockMinimo.ToString());
                                
                                // CORRECCIÓN AQUÍ: Aplicamos color al Text(), no a la celda
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.CantidadFaltante.ToString()).Bold().FontColor(Colors.Red.Medium);
                                
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.CostoPromedioUnitario.ToString("C", esEcCulture));
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.CostoEstimadoReposicion.ToString("C", esEcCulture)).Bold();
                            }
                            table.Cell().ColumnSpan(7).Element(TotalsRowStyle).Row(row =>
                            {
                                row.RelativeItem(9f).AlignRight().Text("Inversión Total Requerida:").Bold();
                                row.RelativeItem(1.5f).AlignRight().Text(data.Sum(x => x.CostoEstimadoReposicion).ToString("C", esEcCulture)).Bold();
                            });
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateAjustesInventarioPdf(IEnumerable<AjusteInventarioReportDto> data, DateTime startDate, DateTime endDate)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Ajustes de Inventario", startDate, endDate));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Fecha");
                                header.Cell().Element(HeaderCellStyle).Text("Responsable");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).Text("Tipo");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Cant.");
                                header.Cell().Element(HeaderCellStyle).Text("Motivo");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.Fecha.ToString("dd/MM/yyyy HH:mm"));
                                table.Cell().Element(DataCellStyle).Text(item.UsuarioResponsable);
                                table.Cell().Element(DataCellStyle).Text(item.ProductoNombre);
                                table.Cell().Element(DataCellStyle).Text(item.TipoAjuste);
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.CantidadAjustada.ToString());
                                table.Cell().Element(DataCellStyle).Text(item.Motivo);
                            }
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        // --- MÉTODOS PRIVADOS AUXILIARES (Sin cambios lógicos) ---

        private void ComposeHeader(IContainer container, string reportTitle, DateTime startDate, DateTime endDate)
        {
            container.PaddingBottom(10).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Column(col =>
                    {
                        var logoPath = Path.Combine(_env.WebRootPath, "logo.png");
                        if (File.Exists(logoPath))
                        {
                            col.Item().Height(50).Width(150).Image(logoPath).FitArea();
                        }
                        col.Item().Text("Aether Tecnologías").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                    });

                    row.RelativeItem(3).AlignRight().Column(col =>
                    {
                        col.Item().Text(reportTitle).Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                        col.Item().Text($"Período: {startDate.ToString("dd/MM/yyyy")} - {endDate.ToString("dd/MM/yyyy")}").FontSize(10);
                        col.Item().Text($"Fecha de Generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}").FontSize(8);
                    });
                });
                column.Item().PaddingTop(5).LineHorizontal(1);
            });
        }

        private void ComposeHeader(IContainer container, string reportTitle)
        {
            container.PaddingBottom(10).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Column(col =>
                    {
                        var logoPath = Path.Combine(_env.WebRootPath, "logo.png");
                        if (File.Exists(logoPath))
                        {
                            col.Item().Height(50).Width(150).Image(logoPath).FitArea();
                        }
                        col.Item().Text("Aether Tecnologías").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                    });

                    row.RelativeItem(3).AlignRight().Column(col =>
                    {
                        col.Item().Text(reportTitle).Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                        col.Item().Text($"Fecha de Generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}").FontSize(8);
                    });
                });
                column.Item().PaddingTop(5).LineHorizontal(1);
            });
        }

        private void ComposeContentVentas(IContainer container, IEnumerable<VentasPorPeriodoDto> data)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Fecha");
                    header.Cell().Element(HeaderCellStyle).Text("Vendedor");
                    header.Cell().Element(HeaderCellStyle).Text("Facturas Emitidas");
                    header.Cell().Element(HeaderCellStyle).Text("Subtotal");
                    header.Cell().Element(HeaderCellStyle).Text("Total IVA");
                    header.Cell().Element(HeaderCellStyle).Text("Total Vendido");
                });

                foreach (var item in data)
                {
                    table.Cell().Element(DataCellStyle).Text(item.Fecha.ToString("dd/MM/yyyy"));
                    table.Cell().Element(DataCellStyle).Text(item.Vendedor);
                    table.Cell().Element(DataCellStyle).AlignRight().Text(item.CantidadFacturas.ToString());
                    table.Cell().Element(DataCellStyle).AlignRight().Text(item.Subtotal.ToString("C", esEcCulture));
                    table.Cell().Element(DataCellStyle).AlignRight().Text(item.TotalIva.ToString("C", esEcCulture));
                    table.Cell().Element(DataCellStyle).AlignRight().Text(item.Total.ToString("C", esEcCulture));
                }

                table.Cell().ColumnSpan(6).Element(TotalsRowStyle).Row(row =>
                {
                    row.RelativeItem(3.5f).AlignRight().Text("Totales:").Bold();
                    row.RelativeItem(1.5f).AlignRight().Text(data.Sum(x => x.CantidadFacturas).ToString()).Bold();
                    row.RelativeItem(2).AlignRight().Text(data.Sum(x => x.Subtotal).ToString("C", esEcCulture)).Bold();
                    row.RelativeItem(1.5f).AlignRight().Text(data.Sum(x => x.TotalIva).ToString("C", esEcCulture)).Bold();
                    row.RelativeItem(2).AlignRight().Text(data.Sum(x => x.Total).ToString("C", esEcCulture)).Bold();
                });
            });
        }

        static IContainer HeaderCellStyle(IContainer container) =>
            container.DefaultTextStyle(x => x.Bold().FontSize(10)).BorderBottom(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(5).AlignCenter();

        static IContainer DataCellStyle(IContainer container) =>
            container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).PaddingHorizontal(5);

        static IContainer TotalsRowStyle(IContainer container) =>
            container.BorderTop(1).BorderBottom(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten4).PaddingVertical(5);
    }
}