        private readonly IWebHostEnvironment _env;
        private readonly ITimeZoneHelper _timeZoneHelper;

        public PdfGeneratorService(IWebHostEnvironment env, ITimeZoneHelper timeZoneHelper)
        {
            _env = env;
            _timeZoneHelper = timeZoneHelper;
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
                            var fecha = factura.FechaAutorizacion.HasValue ? _timeZoneHelper.ConvertUtcToEcuadorTime(factura.FechaAutorizacion.Value) : DateTime.Now;
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
                    col.Item().Row(row => { row.RelativeItem(6).Text($"Fecha Emisión: {_timeZoneHelper.ConvertUtcToEcuadorTime(factura.FechaEmision):dd/MM/yyyy}").FontSize(8); row.RelativeItem(4).Text($"Guía Remisión:").FontSize(8); });
                    col.Item().Text($"Dirección: {factura.ClienteDireccion ?? "N/A"}").FontSize(8);
                });
                column.Item().PaddingVertical(5);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.ConstantColumn(50); columns.ConstantColumn(30); columns.RelativeColumn(); columns.ConstantColumn(50); columns.ConstantColumn(40); columns.ConstantColumn(50); });
                    table.Header(header => { header.Cell().Element(HeaderCellStyle).Text("Cod."); header.Cell().Element(HeaderCellStyle).Text("Cant."); header.Cell().Element(HeaderCellStyle).Text("Descripción"); header.Cell().Element(HeaderCellStyle).Text("P.Unit"); header.Cell().Element(HeaderCellStyle).Text("Desc."); header.Cell().Element(HeaderCellStyle).Text("Total"); });
                    foreach (var item in factura.Items)
                    {
                        table.Cell().Element(CellStyle).Text(item.ProductCode);
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
                            info.Item().Text($"Email: {factura.ClienteEmail ?? "N/A"}").FontSize(7); 
                            info.Item().Text($"Forma de Pago: {factura.FormaDePago}").FontSize(7); 

                            // === AQUÍ ESTÁ LA LÓGICA DE CRÉDITO (PRESERVADA) ===
                            if (factura.FormaDePago == FormaDePago.Credito)
                            {
                                decimal abono = factura.Total - factura.SaldoPendiente;
                                info.Item().PaddingTop(3).Text("DETALLE DE CRÉDITO:").Bold().FontSize(7);
                                
                                info.Item().Row(r => {
                                    r.RelativeItem().Text("Abono Inicial:").FontSize(7);
                                    r.RelativeItem().AlignRight().Text(abono.ToString("N2")).FontSize(7);
                                });
                                
                                info.Item().Row(r => {
                                    r.RelativeItem().Text("Saldo Pendiente:").FontSize(7);
                                    r.RelativeItem().AlignRight().Text(factura.SaldoPendiente.ToString("N2")).FontSize(7).Bold();
                                });
                            }
                        }); 
                    });
                    
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

        // --- LÓGICA NOTA CRÉDITO (CON IMPUESTOS DINÁMICOS) ---
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
                    column.Item().Text("NOTA DE CRÉDITO").Bold().FontSize(12);
                    column.Item().Text($"No. {nc.NumeroNotaCredito}").FontSize(10);

                    column.Item().PaddingTop(5).Text("NÚMERO DE AUTORIZACIÓN").SemiBold();
                    column.Item().Text(nc.NumeroAutorizacion ?? "PENDIENTE").FontSize(8);

                    column.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("FECHA Y HORA DE AUTORIZACIÓN").SemiBold().FontSize(6);
                            var fecha = nc.FechaAutorizacion.HasValue ? _timeZoneHelper.ConvertUtcToEcuadorTime(nc.FechaAutorizacion.Value) : DateTime.Now;
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

        // ======================== RECIBO DE COBRO (NUEVO) ========================
        public byte[] GenerarReciboCobroPdf(CobroDto cobro)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5.Landscape()); // Formato A5 Horizontal (Típico para recibos)
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                    page.Header().Element(compose => ComposeHeaderRecibo(compose, cobro));
                    page.Content().Element(compose => ComposeContentRecibo(compose, cobro));
                    page.Footer().AlignCenter().Text(x => { x.Span("Generado automáticamente por Aether Tecnologías - "); x.CurrentPageNumber(); });
                });
            }).GeneratePdf();
        }

        private void ComposeHeaderRecibo(IContainer container, CobroDto cobro)
        {
            container.Row(row =>
            {
                // Columna Izquierda (Logo y Empresa)
                row.RelativeItem().Column(column =>
                {
                    column.Item().Row(r =>
                    {
                        var logoPath = Path.Combine(_env.WebRootPath, "logo.png");
                        if (File.Exists(logoPath)) r.AutoItem().Height(50).Image(logoPath).FitArea();
                        r.ConstantItem(10);
                        r.RelativeItem().PaddingTop(5).Text("Aether Tecnologías").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                    });
                    column.Item().Text("R.U.C.: 1850641927001").FontSize(8);
                    column.Item().Text("Matriz: AV. BENJAMIN FRANKLIN SNN Y EDWARD JENNER").FontSize(7);
                });

                row.ConstantItem(10);

                // Columna Derecha (Datos del Recibo)
                row.RelativeItem().Element(ContainerBox).Column(column =>
                {
                    column.Item().AlignCenter().Text("COMPROBANTE DE PAGO").Bold().FontSize(12);
                    column.Item().PaddingTop(5).Row(r => {
                        r.RelativeItem().Text("No. Referencia Interna:").FontSize(8);
                        // Usamos los primeros 8 caracteres del ID como número de recibo visual
                        r.RelativeItem().AlignRight().Text($"{cobro.Id.ToString().Substring(0, 8).ToUpper()}").Bold().FontSize(8);
                    });
                    column.Item().Row(r => {
                        r.RelativeItem().Text("Fecha de Pago:").FontSize(8);
                        r.RelativeItem().AlignRight().Text($"{_timeZoneHelper.ConvertUtcToEcuadorTime(cobro.FechaCobro):dd/MM/yyyy HH:mm}").FontSize(8);
                    });
                });
            });
        }

        private void ComposeContentRecibo(IContainer container, CobroDto cobro)
        {
            container.PaddingVertical(10).Column(column =>
            {
                // Caja de Información del Cliente
                column.Item().Element(ContainerBox).Column(col =>
                {
                    col.Item().Text("DATOS DEL PAGO").Bold().FontSize(10);
                    col.Item().LineHorizontal(0.5f);
                    
                    col.Item().PaddingTop(5).Row(row => 
                    { 
                        row.RelativeItem().Text($"Cliente: {cobro.ClienteNombre}").FontSize(9); 
                    });
                    
                    col.Item().Row(row => 
                    { 
                        row.RelativeItem().Text($"Abonado a Factura No.: {cobro.NumeroFactura}").FontSize(9).Bold(); 
                    });

                    col.Item().PaddingTop(5).Row(row => 
                    { 
                        row.RelativeItem(1).Text("Forma de Pago:").Bold();
                        row.RelativeItem(3).Text(cobro.MetodoDePago);
                    });

                    if (!string.IsNullOrEmpty(cobro.Referencia))
                    {
                        col.Item().Row(row => 
                        { 
                            row.RelativeItem(1).Text("Referencia/Lote:").Bold();
                            row.RelativeItem(3).Text(cobro.Referencia);
                        });
                    }
                });

                column.Item().PaddingVertical(10);

                // Tabla de Valor (Simple)
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.ConstantColumn(100); });
                    
                    table.Header(header => {
                        header.Cell().Element(HeaderCellStyle).Text("CONCEPTO");
                        header.Cell().Element(HeaderCellStyle).Text("VALOR");
                    });

                    table.Cell().Element(CellStyle).Text($"Pago/Abono a Factura {cobro.NumeroFactura}");
                    table.Cell().Element(CellStyle).AlignRight().Text(cobro.Monto.ToString("C"));
                    
                    // Total
                    table.Cell().ColumnSpan(2).PaddingTop(5).AlignRight().Text($"TOTAL PAGADO: {cobro.Monto:C}").Bold().FontSize(12);
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
                        row.RelativeItem(6).Text($"Fecha Emisión: {_timeZoneHelper.ConvertUtcToEcuadorTime(nc.FechaEmision):dd/MM/yyyy}").FontSize(8);
                        row.RelativeItem(4).Text($"Dirección: {nc.ClienteDireccion}").FontSize(8);
                    });
                    
                    col.Item().PaddingTop(5).LineHorizontal(0.5f);
                    col.Item().PaddingTop(2).Text("Comprobante Modificado").Bold().FontSize(8);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Factura: {nc.NumeroFacturaModificada}").FontSize(8);
                        row.RelativeItem().Text($"Fecha Sustento: {_timeZoneHelper.ConvertUtcToEcuadorTime(nc.FechaEmisionFacturaModificada):dd/MM/yyyy}").FontSize(8);
                    });
                    col.Item().Text($"Razón Modificación: {nc.RazonModificacion}").FontSize(8).Italic();
                });

                column.Item().PaddingVertical(5);

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

                    foreach (var item in nc.Items)
                    {
                        table.Cell().Element(CellStyle).Text(item.ProductCode);
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

                    // === TOTALES DE NOTA DE CRÉDITO (DINÁMICOS) ===
                    row.RelativeItem(4).Element(ContainerBox).Column(c =>
                    {
                        // 1. Subtotal Sin Impuestos
                        TotalesRow(c, "SUBTOTAL SIN IMPUESTOS", nc.SubtotalSinImpuestos);

                        // 2. Bases Imponibles (Iteramos sobre los impuestos calculados en el servicio)
                        if (nc.TaxSummaries != null)
                        {
                            foreach (var tax in nc.TaxSummaries)
                            {
                                decimal baseImponible = 0;
                                // Intentamos calcular la base a partir del monto del impuesto
                                if (tax.TaxRate > 0)
                                    baseImponible = tax.Amount / (tax.TaxRate / 100m);
                                else
                                    // Si es 0%, es el remanente (aproximación)
                                    baseImponible = nc.SubtotalSinImpuestos - nc.TaxSummaries.Where(t => t.TaxRate > 0).Sum(t => t.Amount / (t.TaxRate / 100m)); 
                                
                                TotalesRow(c, $"SUBTOTAL {tax.TaxRate:0.#}%", baseImponible);
                            }

                            // 3. Valores de Impuestos (Solo los > 0)
                            foreach (var tax in nc.TaxSummaries.Where(x => x.TaxRate > 0))
                            {
                                TotalesRow(c, $"{tax.TaxName} {tax.TaxRate:0.#}%", tax.Amount);
                            }
                        }
                        else 
                        {
                            // Fallback si por alguna razón no llegaron summaries
                            TotalesRow(c, "TOTAL IVA", nc.TotalIVA);
                        }

                        // 4. Total Final
                        c.Item().PaddingTop(2).BorderTop(1).Row(r =>
                        {
                            r.RelativeItem().Text("VALOR TOTAL").Bold();
                            r.RelativeItem().AlignRight().Text(nc.Total.ToString("N2")).Bold();
                        });
                    });
                });
            });
        }
