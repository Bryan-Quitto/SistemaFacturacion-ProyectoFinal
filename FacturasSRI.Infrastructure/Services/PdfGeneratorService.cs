using FacturasSRI.Application.Dtos;
using Microsoft.AspNetCore.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace FacturasSRI.Infrastructure.Services
{
    public class PdfGeneratorService
    {
        private readonly IWebHostEnvironment _env;

        public PdfGeneratorService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public byte[] GenerarFacturaPdf(InvoiceDetailViewDto factura)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.Arial));

                    page.Header().Element(compose => ComposeHeader(compose, factura));
                    page.Content().Element(compose => ComposeContent(compose, factura));
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return documento.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, InvoiceDetailViewDto factura)
        {
            container.Row(row =>
            {
                // --- COLUMNA IZQUIERDA ---
                row.RelativeItem().Column(column =>
                {
                    // Logo y Nombre
                    column.Item().Row(r =>
                    {
                        var logoPath = Path.Combine(_env.WebRootPath, "logo.png");
                        if (File.Exists(logoPath))
                        {
                            r.AutoItem().Height(70).Image(logoPath).FitArea();
                        }

                        r.ConstantItem(10);

                        r.RelativeItem().PaddingTop(10).Text("AETHER TECH")
                            .Bold().FontSize(16).FontColor(Colors.Blue.Darken2); 
                    });

                    column.Item().PaddingBottom(5);

                    // Info Emisor
                    column.Item().Element(ContainerBox).Column(col =>
                    {
                        col.Item().Text("AÑILEMA HOFFMANN JIMMY ALEXANDER").Bold().FontSize(10);
                        col.Item().Text("Dirección Matriz: AV. BENJAMIN FRANKLIN SNN Y EDWARD JENNER").FontSize(7);
                        col.Item().Text("Dirección Sucursal: AV. BENJAMIN FRANKLIN SNN Y EDWARD JENNER").FontSize(7);
                        col.Item().Text("OBLIGADO A LLEVAR CONTABILIDAD: NO").FontSize(8).SemiBold();
                    });
                });

                row.ConstantItem(10);

                // --- COLUMNA DERECHA (RIDE) ---
                row.RelativeItem().Element(ContainerBox).Column(column =>
                {
                    column.Item().Text("R.U.C.: 1850641927001").Bold().FontSize(12);
                    column.Item().Text("FACTURA").Bold().FontSize(12);
                    column.Item().Text($"No. {factura.NumeroFactura}").FontSize(10);

                    column.Item().PaddingTop(5).Text("NÚMERO DE AUTORIZACIÓN").SemiBold();
                    column.Item().Text(factura.NumeroAutorizacion ?? "PENDIENTE").FontSize(8);

                    column.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("FECHA Y HORA DE AUTORIZACIÓN").SemiBold().FontSize(6);
                            var fechaAuth = factura.FechaEmision; 
                            c.Item().Text($"{fechaAuth:dd/MM/yyyy HH:mm:ss}").FontSize(8);
                        });
                    });

                    column.Item().PaddingTop(5).Text("AMBIENTE: PRUEBAS").FontSize(8);
                    column.Item().Text("EMISIÓN: NORMAL").FontSize(8);

                    column.Item().PaddingTop(5).Text("CLAVE DE ACCESO").SemiBold();
                    var claveAcceso = factura.ClaveAcceso ?? "0000000000000000000000000000000000000000000000000";
                    column.Item().Text(claveAcceso).FontSize(8);

                    // --- CÓDIGO DE BARRAS ---
                    // Generamos los bytes del código de barras
                    byte[] barcodeBytes = GenerarCodigoBarras(claveAcceso);
                    
                    column.Item().PaddingTop(10)
                          .AlignCenter()
                          .Height(40) // Altura fija para las barras
                          .Image(barcodeBytes)
                          .FitArea(); // Ajusta la imagen al ancho disponible sin deformarla
                });
            });
        }

        // --- MÉTODO ACTUALIZADO PARA BARRAS ---
        private byte[] GenerarCodigoBarras(string texto)
        {
            // BarcodeLib requiere un objeto Barcode
            var codigo = new BarcodeLib.Barcode.Barcode();
            
            // Configuración
            codigo.IncludeLabel = false; // Ponemos false porque ya escribimos la clave arriba con texto normal
            codigo.Alignment = BarcodeLib.Barcode.AlignmentPositions.CENTER;
            
            // Generar (Tipo Code 128 es el estándar para SRI)
            // Ancho 400px, Alto 100px (proporción rectangular)
            try 
            {
                // El SRI usa 49 dígitos, Code128 es el único que lo soporta bien
                var imagen = codigo.Encode(BarcodeLib.Barcode.TYPE.CODE128, texto, 400, 80);
                
                // Obtenemos los bytes en formato PNG
                return codigo.GetImageData(BarcodeLib.Barcode.SaveTypes.PNG);
            }
            catch
            {
                // Si falla (ej. texto vacío), devolvemos una imagen vacía o lanzamos error
                return new byte[0];
            }
        }

        // ... (El resto del método ComposeContent y los estilos se mantienen igual) ...
        
        private void ComposeContent(IContainer container, InvoiceDetailViewDto factura)
        {
             container.PaddingVertical(10).Column(column =>
            {
                // Datos Cliente
                column.Item().Element(ContainerBox).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(6).Text($"Razón Social: {factura.ClienteNombre}").FontSize(8);
                        row.RelativeItem(4).Text($"Identificación: {factura.ClienteIdentificacion}").FontSize(8);
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(6).Text($"Fecha Emisión: {factura.FechaEmision:dd/MM/yyyy}").FontSize(8);
                        row.RelativeItem(4).Text($"Guía Remisión:").FontSize(8); 
                    });
                    col.Item().Text($"Dirección: {factura.ClienteDireccion ?? "N/A"}").FontSize(8);
                });

                column.Item().PaddingVertical(5);

                // Tabla Detalle
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(50); 
                        columns.ConstantColumn(30); 
                        columns.RelativeColumn();   
                        columns.ConstantColumn(50); 
                        columns.ConstantColumn(40); 
                        columns.ConstantColumn(50); 
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCellStyle).Text("Cod.");
                        header.Cell().Element(HeaderCellStyle).Text("Cant.");
                        header.Cell().Element(HeaderCellStyle).Text("Descripción");
                        header.Cell().Element(HeaderCellStyle).Text("P.Unit");
                        header.Cell().Element(HeaderCellStyle).Text("Desc.");
                        header.Cell().Element(HeaderCellStyle).Text("Total");
                    });

                    foreach (var item in factura.Items)
                    {
                        table.Cell().Element(CellStyle).Text("PROD");
                        table.Cell().Element(CellStyle).AlignCenter().Text(item.Cantidad.ToString("N2"));
                        table.Cell().Element(CellStyle).Text(item.ProductName);
                        table.Cell().Element(CellStyle).AlignRight().Text(item.PrecioVentaUnitario.ToString("N2"));
                        table.Cell().Element(CellStyle).AlignRight().Text("0.00");
                        table.Cell().Element(CellStyle).AlignRight().Text(item.Subtotal.ToString("N2"));
                    }
                });

                // Footer (Info y Totales)
                column.Item().PaddingTop(5).Row(row =>
                {
                    // Info Adicional
                    row.RelativeItem(6).Column(c =>
                    {
                        c.Item().Element(ContainerBox).Column(info =>
                        {
                            info.Item().Text("Información Adicional").Bold().FontSize(8);
                            info.Item().Text($"Email: {factura.ClienteEmail ?? "N/A"}").FontSize(7);
                            info.Item().Text("Forma de Pago: UTILIZACION DEL SISTEMA FINANCIERO").FontSize(7);
                        });
                    });

                    row.ConstantItem(10);

                    // Totales
                    row.RelativeItem(4).Element(ContainerBox).Column(c =>
                    {
                        decimal totalIva = factura.TotalIVA;
                        decimal baseImponible15 = 0;
                        
                        var summary15 = factura.TaxSummaries.FirstOrDefault(x => x.TaxRate > 0);
                        if (summary15 != null && summary15.TaxRate > 0)
                        {
                            baseImponible15 = summary15.Amount / (summary15.TaxRate / 100m);
                        }

                        decimal baseImponible0 = factura.SubtotalSinImpuestos - baseImponible15;
                        if (baseImponible0 < 0) baseImponible0 = 0;

                        TotalesRow(c, "SUBTOTAL 15%", baseImponible15);
                        TotalesRow(c, "SUBTOTAL 0%", baseImponible0);
                        TotalesRow(c, "SUBTOTAL No objeto de IVA", 0);
                        TotalesRow(c, "SUBTOTAL Exento de IVA", 0);
                        TotalesRow(c, "SUBTOTAL SIN IMPUESTOS", factura.SubtotalSinImpuestos);
                        TotalesRow(c, "TOTAL Descuento", 0);
                        TotalesRow(c, "IVA 15%", totalIva);
                        TotalesRow(c, "PROPINA", 0);

                        c.Item().PaddingTop(2).BorderTop(1).Row(r =>
                        {
                            r.RelativeItem().Text("VALOR TOTAL").Bold();
                            r.RelativeItem().AlignRight().Text(factura.Total.ToString("N2")).Bold();
                        });
                    });
                });
            });
        }

        // --- ESTILOS ---
        static IContainer ContainerBox(IContainer container)
        {
            return container.Border(1).BorderColor(Colors.Black).Padding(4);
        }

        static IContainer HeaderCellStyle(IContainer container)
        {
            return container.Border(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten3).Padding(2).AlignCenter();
        }

        static IContainer CellStyle(IContainer container)
        {
            return container.BorderLeft(1).BorderRight(1).BorderBottom(1).BorderColor(Colors.Black).Padding(2);
        }

        void TotalesRow(ColumnDescriptor column, string label, decimal value)
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontSize(7);
                row.RelativeItem().AlignRight().Text(value.ToString("N2")).FontSize(7);
            });
        }
    }
}