using FacturasSRI.Application.Dtos.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting; // Needed for IWebHostEnvironment
using System.Globalization; // For CultureInfo

namespace FacturasSRI.Infrastructure.Services
{
    public class ReportPdfGeneratorService
    {
        private readonly IWebHostEnvironment _env;
        private readonly CultureInfo esEcCulture = new CultureInfo("es-EC");

        public ReportPdfGeneratorService(IWebHostEnvironment env)
        {
            _env = env;
            QuestPDF.Settings.License = LicenseType.Community; // Ensure license is set
        }

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
                    page.Content().Element(compose => ComposeContent(compose, data));
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        private void ComposeHeader(IContainer container, string reportTitle, DateTime startDate, DateTime endDate)
        {
            container.PaddingBottom(10).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem(2).Column(col => // Increased relative size for logo/company name
                    {
                        var logoPath = Path.Combine(_env.WebRootPath, "logo.png");
                        if (File.Exists(logoPath))
                        {
                            col.Item().Height(50).Width(150).Image(logoPath).FitArea();
                        }
                        col.Item().Text("Aether Tecnologías").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                    });

                    row.RelativeItem(3).AlignRight().Column(col => // Report title and dates on the right
                    {
                        col.Item().Text(reportTitle).Bold().FontSize(16).FontColor(Colors.Blue.Darken2); // Changed color from Red to Blue
                        col.Item().Text($"Período: {startDate.ToString("dd/MM/yyyy")} - {endDate.ToString("dd/MM/yyyy")}").FontSize(10);
                        col.Item().Text($"Fecha de Generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}").FontSize(8);
                    });
                });
                column.Item().PaddingTop(5).LineHorizontal(1); // Slightly thicker line below header
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

        private void ComposeContent(IContainer container, IEnumerable<VentasPorPeriodoDto> data)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.5f); // Fecha
                    columns.RelativeColumn(2); // Vendedor
                    columns.RelativeColumn(1.5f); // CantidadFacturas
                    columns.RelativeColumn(2); // Subtotal
                    columns.RelativeColumn(1.5f); // TotalIva
                    columns.RelativeColumn(2); // Total
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Fecha");
                    header.Cell().Element(HeaderCellStyle).Text("Vendedor");
                    header.Cell().Element(HeaderCellStyle).Text("Facturas Emitidas");
                    header.Cell().Element(HeaderCellStyle).Text("Subtotal");
                    header.Cell().Element(HeaderCellStyle).Text("Total IVA");
                    header.Cell().Element(HeaderCellStyle).Text("Total Vendido");

                    // No need for LineHorizontal here, HeaderCellStyle takes care of bottom border
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
                    row.RelativeItem(3.5f).AlignRight().Text("Totales:").Bold(); // Adjusted to span Fecha + Vendedor columns
                    row.RelativeItem(1.5f).AlignRight().Text(data.Sum(x => x.CantidadFacturas).ToString()).Bold();
                    row.RelativeItem(2).AlignRight().Text(data.Sum(x => x.Subtotal).ToString("C", esEcCulture)).Bold();
                    row.RelativeItem(1.5f).AlignRight().Text(data.Sum(x => x.TotalIva).ToString("C", esEcCulture)).Bold();
                    row.RelativeItem(2).AlignRight().Text(data.Sum(x => x.Total).ToString("C", esEcCulture)).Bold();
                });
            });
        }

        // Helper styles
        static IContainer HeaderCellStyle(IContainer container) => 
            container.DefaultTextStyle(x => x.Bold().FontSize(10)).BorderBottom(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(5).AlignCenter();
        
        static IContainer DataCellStyle(IContainer container) => 
            container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).PaddingHorizontal(5); // Lighter border for data rows

        static IContainer TotalsRowStyle(IContainer container) => 
            container.BorderTop(1).BorderBottom(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten4).PaddingVertical(5);

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
                                columns.ConstantColumn(60); // Codigo
                                columns.RelativeColumn(1.5f); // Producto
                                columns.RelativeColumn(); // Fecha
                                columns.RelativeColumn(); // Vendedor
                                columns.RelativeColumn(); // Cantidad Vendida
                                columns.RelativeColumn(); // Precio Promedio
                                columns.RelativeColumn(); // Total Vendido
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
                                row.RelativeItem(3.1f).AlignRight().Text("Total:").Bold(); // Corresponds to Codigo + Producto + Fecha + Vendedor roughly
                                row.RelativeItem(1).AlignRight().Text(data.Sum(x => x.CantidadVendida).ToString("N2")).Bold();
                                row.RelativeItem(1); // Spacer for Precio Promedio
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
                    page.Size(PageSizes.A4.Landscape()); // Use landscape for more columns
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
                    page.Size(PageSizes.A4.Landscape()); // Use landscape
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Cuentas por Cobrar")); // Use new header
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2); // Vendedor
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
                                row.RelativeItem(11.5f).AlignRight().Text("Total Pendiente:").Bold(); // Spans first 6 columns
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
                    page.Size(PageSizes.A4.Landscape()); // Use landscape
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
                                columns.RelativeColumn(2); // Vendedor
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
                                row.RelativeItem(13.5f).AlignRight().Text("Total Devuelto:").Bold(); // Adjusted to span 6 columns for the label
                                row.RelativeItem(2).AlignRight().Text(data.Sum(x => x.ValorTotal).ToString("C", esEcCulture)).Bold();
                            });
                        });
                    });
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        public byte[] GenerateStockActualPdf(IEnumerable<StockActualDto> data)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Stock Actual"));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(75);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Código");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).Text("Categoría");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Stock Actual");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Costo Promedio");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Valor Inventario");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.CodigoPrincipal);
                                table.Cell().Element(DataCellStyle).Text(item.NombreProducto);
                                table.Cell().Element(DataCellStyle).Text(item.Categoria);
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.StockTotal.ToString());
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.PrecioCompraPromedioPonderado.ToString("C", esEcCulture));
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.ValorInventario.ToString("C", esEcCulture)).Bold();
                            }
                            table.Cell().ColumnSpan(6).Element(TotalsRowStyle).Row(row =>
                            {
                                row.RelativeItem(8.5f).AlignRight().Text("Valor Total del Inventario:").Bold();
                                row.RelativeItem(2).AlignRight().Text(data.Sum(x => x.ValorInventario).ToString("C", esEcCulture)).Bold();
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
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Movimientos de Inventario", startDate, endDate));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Fecha");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).Text("Tipo de Movimiento");
                                header.Cell().Element(HeaderCellStyle).Text("Documento / Motivo");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Cantidad");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.Fecha.ToString("dd/MM/yyyy HH:mm"));
                                table.Cell().Element(DataCellStyle).Text(item.ProductoNombre);
                                table.Cell().Element(DataCellStyle).Text(item.TipoMovimiento);
                                table.Cell().Element(DataCellStyle).Text(item.DocumentoReferencia);
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.Cantidad.ToString()).Bold();
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
                    // CAMBIO IMPORTANTE: Landscape para que quepan las columnas
                    page.Size(PageSizes.A4.Landscape()); 
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte Detallado de Compras", startDate, endDate));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f); // Fecha
                                columns.RelativeColumn(2.5f); // Proveedor
                                columns.RelativeColumn(2.5f); // Documento (Fac + Int)
                                columns.RelativeColumn(3);    // Producto
                                columns.RelativeColumn(1);    // Cantidad
                                columns.RelativeColumn(1.5f); // Costo Unit
                                columns.RelativeColumn(1.5f); // Total
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Fecha");
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
                                table.Cell().Element(DataCellStyle).Text(item.NombreProveedor);
                                
                                // Combinamos Fac Prov e Interno en la misma celda
                                table.Cell().Element(DataCellStyle).Column(col => 
                                {
                                    if(!string.IsNullOrEmpty(item.NumeroFacturaProveedor))
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
                                row.RelativeItem(10.5f).AlignRight().Text("Totales:").Bold(); // Ajustado para alinear con columnas anteriores
                                row.RelativeItem(1).AlignRight().Text(data.Sum(x => x.CantidadComprada).ToString("N0")).Bold();
                                row.RelativeItem(1.5f); // Espacio vacío bajo Costo Unit
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
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Element(compose => ComposeHeader(compose, "Reporte de Productos Bajo Stock Mínimo"));
                    page.Content().Element(compose =>
                    {
                        compose.Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(75);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Código");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Stock Actual");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Stock Mínimo");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Cantidad Faltante");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.CodigoPrincipal);
                                table.Cell().Element(DataCellStyle).Text(item.NombreProducto);
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.StockActual.ToString());
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.StockMinimo.ToString());
                                table.Cell().Element(DataCellStyle).AlignRight().Text(item.CantidadFaltante.ToString()).Bold();
                            }
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
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(3);
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Fecha");
                                header.Cell().Element(HeaderCellStyle).Text("Producto");
                                header.Cell().Element(HeaderCellStyle).Text("Tipo de Ajuste");
                                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Cantidad Ajustada");
                                header.Cell().Element(HeaderCellStyle).Text("Motivo");

                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(DataCellStyle).Text(item.Fecha.ToString("dd/MM/yyyy HH:mm"));
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
    }
}
