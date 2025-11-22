using FacturasSRI.Application.Dtos;
using FacturasSRI.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Linq;
using SkiaSharp;
using ZXing;
using ZXing.SkiaSharp;
using ZXing.Common;

namespace FacturasSRI.Infrastructure.Services
{
    public class PdfGeneratorService
    {
        private readonly IWebHostEnvironment _env;

        public PdfGeneratorService(IWebHostEnvironment env)
        {
            _env = env;
        }

        // ======================== FACTURA ========================
        public byte[] GenerarFacturaPdf(InvoiceDetailViewDto factura)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.Arial));

                    page.Header().Element(compose => ComposeHeader(compose, factura));
                    page.Content().Element(compose => ComposeContent(compose, factura));
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        // ======================== NOTA DE CRÉDITO ========================
        public byte[] GenerarNotaCreditoPdf(CreditNoteDetailViewDto nc)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.Arial));

                    page.Header().Element(compose => ComposeHeaderNC(compose, nc));
                    page.Content().Element(compose => ComposeContentNC(compose, nc));
                    page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            }).GeneratePdf();
        }

        // --- LÓGICA FACTURA ---
        private void ComposeHeader(IContainer container, InvoiceDetailViewDto factura)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Row(r =>
                    {
                        var logoPath = Path.Combine(_env.WebRootPath, "logo.png");
                        if (File.Exists(logoPath)) r.AutoItem().Height(70).Image(logoPath).FitArea();
                        r.ConstantItem(10);
                        r.RelativeItem().PaddingTop(10).Text("Aether Tecnologías").Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                    });
                    column.Item().PaddingBottom(5);
                    column.Item().Element(ContainerBox).Column(col =>
                    {
                        col.Item().Text("AÑILEMA HOFFMANN JIMMY ALEXANDER").Bold().FontSize(10);
                        col.Item().Text("Dirección Matriz: AV. BENJAMIN FRANKLIN SNN Y EDWARD JENNER").FontSize(7);
                        col.Item().Text("Dirección Sucursal: AV. BENJAMIN FRANKLIN SNN Y EDWARD JENNER").FontSize(7);
                        col.Item().Text("OBLIGADO A LLEVAR CONTABILIDAD: NO").FontSize(8).SemiBold();
                    });
                });
                row.ConstantItem(10);
                row.RelativeItem().Element(ContainerBox).Column(column =>
                {
                    column.Item().Text("R.U.C.: 1850641927001").Bold().FontSize(12);
                    column.Item().Text("FACTURA").Bold().FontSize(12);
                    column.Item().Text($"No. {factura.NumeroFactura}").FontSize(10);
                    column.Item().PaddingTop(5).Text("NÚMERO DE AUTORIZACIÓN").SemiBold();
                    column.Item().Text(factura.NumeroAutorizacion ?? "PENDIENTE").FontSize(8);
                    column.Item().PaddingTop(5).Row(r => {
                        r.RelativeItem().Column(c => {
                            c.Item().Text("FECHA Y HORA DE AUTORIZACIÓN").SemiBold().FontSize(6);
                            var fecha = factura.FechaEmision == default ? DateTime.Now : factura.FechaEmision;
                            c.Item().Text($"{fecha:dd/MM/yyyy HH:mm:ss}").FontSize(8);
                        });
                    });
                    column.Item().PaddingTop(5).Text("AMBIENTE: PRUEBAS").FontSize(8);
                    column.Item().Text("EMISIÓN: NORMAL").FontSize(8);
                    column.Item().PaddingTop(5).Text("CLAVE DE ACCESO").SemiBold();
                    var claveAcceso = factura.ClaveAcceso ?? "0000000000000000000000000000000000000000000000000";
                    column.Item().Text(claveAcceso).FontSize(8);
                    byte[] barcodeBytes = GenerarCodigoBarras(claveAcceso);
                    if (barcodeBytes.Length > 0) column.Item().PaddingTop(10).AlignCenter().Height(40).Image(barcodeBytes).FitArea();
                    else column.Item().PaddingTop(10).Text("[ERROR GENERANDO BARRAS]").FontColor(Colors.Red.Medium);
                });
            });
        }

        private void ComposeContent(IContainer container, InvoiceDetailViewDto factura)
        {
             container.PaddingVertical(10).Column(column =>
            {
                column.Item().Element(ContainerBox).Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem(6).Text($"Razón Social: {factura.ClienteNombre}").FontSize(8); row.RelativeItem(4).Text($"Identificación: {factura.ClienteIdentificacion}").FontSize(8); });
                    col.Item().Row(row => { row.RelativeItem(6).Text($"Fecha Emisión: {factura.FechaEmision:dd/MM/yyyy}").FontSize(8); row.RelativeItem(4).Text($"Guía Remisión:").FontSize(8); });
                    col.Item().Text($"Dirección: {factura.ClienteDireccion ?? "N/A"}").FontSize(8);
                });
                column.Item().PaddingVertical(5);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.ConstantColumn(50); columns.ConstantColumn(30); columns.RelativeColumn(); columns.ConstantColumn(50); columns.ConstantColumn(40); columns.ConstantColumn(50); });
                    table.Header(header => { header.Cell().Element(HeaderCellStyle).Text("Cod."); header.Cell().Element(HeaderCellStyle).Text("Cant."); header.Cell().Element(HeaderCellStyle).Text("Descripción"); header.Cell().Element(HeaderCellStyle).Text("P.Unit"); header.Cell().Element(HeaderCellStyle).Text("Desc."); header.Cell().Element(HeaderCellStyle).Text("Total"); });
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
                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem(6).Column(c => { c.Item().Element(ContainerBox).Column(info => { info.Item().Text("Información Adicional").Bold().FontSize(8); info.Item().Text($"Email: {factura.ClienteEmail ?? "N/A"}").FontSize(7); info.Item().Text($"Forma de Pago: {factura.FormaDePago}").FontSize(7); }); });
                    row.ConstantItem(10);
                    row.RelativeItem(4).Element(ContainerBox).Column(c => {
                        decimal totalIva = factura.TotalIVA;
                        decimal baseImponible15 = 0;
                        var summary15 = factura.TaxSummaries.FirstOrDefault(x => x.TaxRate > 0);
                        if (summary15 != null && summary15.TaxRate > 0) baseImponible15 = summary15.Amount / (summary15.TaxRate / 100m);
                        decimal baseImponible0 = factura.SubtotalSinImpuestos - baseImponible15;
                        if (baseImponible0 < 0) baseImponible0 = 0;
                        TotalesRow(c, "SUBTOTAL 15%", baseImponible15); TotalesRow(c, "SUBTOTAL 0%", baseImponible0); TotalesRow(c, "SUBTOTAL SIN IMPUESTOS", factura.SubtotalSinImpuestos); TotalesRow(c, "IVA 15%", totalIva);
                        c.Item().PaddingTop(2).BorderTop(1).Row(r => { r.RelativeItem().Text("VALOR TOTAL").Bold(); r.RelativeItem().AlignRight().Text(factura.Total.ToString("N2")).Bold(); });
                    });
                });
            });
        }

        // --- LÓGICA NOTA CRÉDITO (NUEVA) ---
        private void ComposeHeaderNC(IContainer container, CreditNoteDetailViewDto nc)
        {
             container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Row(r =>
                    {
                        var logoPath = Path.Combine(_env.WebRootPath, "logo.png");
                        if (File.Exists(logoPath)) r.AutoItem().Height(70).Image(logoPath).FitArea();
                        r.ConstantItem(10);
                        r.RelativeItem().PaddingTop(10).Text("Aether Tecnologías").Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                    });
                    column.Item().PaddingBottom(5);
                    column.Item().Element(ContainerBox).Column(col =>
                    {
                        col.Item().Text("AÑILEMA HOFFMANN JIMMY ALEXANDER").Bold().FontSize(10);
                        col.Item().Text("Dirección Matriz: AV. BENJAMIN FRANKLIN SNN Y EDWARD JENNER").FontSize(7);
                        col.Item().Text("Dirección Sucursal: AV. BENJAMIN FRANKLIN SNN Y EDWARD JENNER").FontSize(7);
                        col.Item().Text("OBLIGADO A LLEVAR CONTABILIDAD: NO").FontSize(8).SemiBold();
                    });
                });

                row.ConstantItem(10);

                row.RelativeItem().Element(ContainerBox).Column(column =>
                {
                    column.Item().Text("R.U.C.: 1850641927001").Bold().FontSize(12);
                    // CAMBIO CLAVE: NOTA DE CRÉDITO
                    column.Item().Text("NOTA DE CRÉDITO").Bold().FontSize(12);
                    column.Item().Text($"No. {nc.NumeroNotaCredito}").FontSize(10);

                    column.Item().PaddingTop(5).Text("NÚMERO DE AUTORIZACIÓN").SemiBold();
                    column.Item().Text(nc.NumeroAutorizacion ?? "PENDIENTE").FontSize(8);

                    column.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("FECHA Y HORA DE AUTORIZACIÓN").SemiBold().FontSize(6);
                            var fecha = nc.FechaEmision == default ? DateTime.Now : nc.FechaEmision;
                            c.Item().Text($"{fecha:dd/MM/yyyy HH:mm:ss}").FontSize(8);
                        });
                    });

                    column.Item().PaddingTop(5).Text("AMBIENTE: PRUEBAS").FontSize(8);
                    column.Item().Text("EMISIÓN: NORMAL").FontSize(8);
                    column.Item().PaddingTop(5).Text("CLAVE DE ACCESO").SemiBold();
                    
                    var claveAcceso = nc.ClaveAcceso ?? "0000000000000000000000000000000000000000000000000";
                    column.Item().Text(claveAcceso).FontSize(8);

                    byte[] barcodeBytes = GenerarCodigoBarras(claveAcceso);
                    if (barcodeBytes.Length > 0) column.Item().PaddingTop(10).AlignCenter().Height(40).Image(barcodeBytes).FitArea();
                    else column.Item().PaddingTop(10).Text("[ERROR GENERANDO BARRAS]").FontColor(Colors.Red.Medium);
                });
            });
        }

        private void ComposeContentNC(IContainer container, CreditNoteDetailViewDto nc)
        {
            container.PaddingVertical(10).Column(column =>
            {
                column.Item().Element(ContainerBox).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(6).Text($"Razón Social: {nc.ClienteNombre}").FontSize(8);
                        row.RelativeItem(4).Text($"Identificación: {nc.ClienteIdentificacion}").FontSize(8);
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(6).Text($"Fecha Emisión: {nc.FechaEmision:dd/MM/yyyy}").FontSize(8);
                        row.RelativeItem(4).Text($"Dirección: {nc.ClienteDireccion}").FontSize(8);
                    });
                    
                    // SECCION ESPECIFICA DE NC: DOCUMENTO MODIFICADO
                    col.Item().PaddingTop(5).LineHorizontal(0.5f);
                    col.Item().PaddingTop(2).Text("Comprobante Modificado").Bold().FontSize(8);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Factura: {nc.NumeroFacturaModificada}").FontSize(8);
                        row.RelativeItem().Text($"Fecha Sustento: {nc.FechaEmisionFacturaModificada:dd/MM/yyyy}").FontSize(8);
                    });
                    col.Item().Text($"Razón Modificación: {nc.RazonModificacion}").FontSize(8).Italic();
                });

                column.Item().PaddingVertical(5);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(50); // Cod
                        columns.ConstantColumn(30); // Cant
                        columns.RelativeColumn();   // Desc
                        columns.ConstantColumn(50); // P.Unit
                        columns.ConstantColumn(40); // Desc
                        columns.ConstantColumn(50); // Total
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

                    foreach (var item in nc.Items)
                    {
                        table.Cell().Element(CellStyle).Text("PROD");
                        table.Cell().Element(CellStyle).AlignCenter().Text(item.Cantidad.ToString("N2"));
                        table.Cell().Element(CellStyle).Text(item.ProductName);
                        table.Cell().Element(CellStyle).AlignRight().Text(item.PrecioVentaUnitario.ToString("N2"));
                        table.Cell().Element(CellStyle).AlignRight().Text("0.00");
                        table.Cell().Element(CellStyle).AlignRight().Text(item.Subtotal.ToString("N2"));
                    }
                });

                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem(6).Column(c =>
                    {
                        c.Item().Element(ContainerBox).Column(info =>
                        {
                            info.Item().Text("Información Adicional").Bold().FontSize(8);
                            info.Item().Text($"Email: {nc.ClienteEmail ?? "N/A"}").FontSize(7);
                            info.Item().Text("Documento generado electrónicamente").FontSize(7);
                        });
                    });

                    row.ConstantItem(10);

                    row.RelativeItem(4).Element(ContainerBox).Column(c =>
                    {
                        // Lógica simplificada de totales para NC (replicando factura)
                        decimal totalIva = nc.TotalIVA;
                        decimal baseImponible15 = 0;
                        
                        // Si tienes TaxSummaries en NC, úsalos. Si no, calculo aprox para el PDF visual
                        // Asumo que la mayor parte es IVA 15%
                        if (totalIva > 0) baseImponible15 = totalIva / 0.15m;
                        
                        decimal baseImponible0 = nc.SubtotalSinImpuestos - baseImponible15;
                        if (baseImponible0 < 0) baseImponible0 = 0;

                        TotalesRow(c, "SUBTOTAL 15%", baseImponible15);
                        TotalesRow(c, "SUBTOTAL 0%", baseImponible0);
                        TotalesRow(c, "SUBTOTAL SIN IMPUESTOS", nc.SubtotalSinImpuestos);
                        TotalesRow(c, "IVA 15%", totalIva);

                        c.Item().PaddingTop(2).BorderTop(1).Row(r =>
                        {
                            r.RelativeItem().Text("VALOR TOTAL").Bold();
                            r.RelativeItem().AlignRight().Text(nc.Total.ToString("N2")).Bold();
                        });
                    });
                });
            });
        }

        // --- Helpers Compartidos ---
        private byte[] GenerarCodigoBarras(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return Array.Empty<byte>();
            try
            {
                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.CODE_128,
                    Options = new EncodingOptions { Width = 400, Height = 60, PureBarcode = true, Margin = 0 }
                };
                var bitmap = writer.Write(texto);
                using (var image = SKImage.FromBitmap(bitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }
            catch (Exception) { return Array.Empty<byte>(); }
        }

        static IContainer ContainerBox(IContainer container) => container.Border(1).BorderColor(Colors.Black).Padding(4);
        static IContainer HeaderCellStyle(IContainer container) => container.Border(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten3).Padding(2).AlignCenter();
        static IContainer CellStyle(IContainer container) => container.BorderLeft(1).BorderRight(1).BorderBottom(1).BorderColor(Colors.Black).Padding(2);

        void TotalesRow(ColumnDescriptor column, string label, decimal value)
        {
            column.Item().Row(row => { row.RelativeItem().Text(label).FontSize(7); row.RelativeItem().AlignRight().Text(value.ToString("N2")).FontSize(7); });
        }
    }
}