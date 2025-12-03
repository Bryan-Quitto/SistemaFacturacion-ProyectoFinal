using FacturasSRI.Application.Interfaces;
using FacturasSRI.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;

namespace FacturasSRI.Web.Endpoints
{
    public static class ReportEndpoints
    {
        public static void MapReportEndpoints(this IEndpointRouteBuilder app)
        {
            var reportGroup = app.MapGroup("/api/reports").WithTags("Reports");

            reportGroup.MapGet("/sales/by-period", async (IReportService reportService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();

                var result = await reportService.GetVentasPorPeriodoAsync(finalStartDate, finalEndDate);
                return Results.Ok(result);
            })
            .WithName("GetSalesByPeriodReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.VentasPorPeriodoDto>));

            reportGroup.MapGet("/sales/by-product", async (IReportService reportService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();

                var result = await reportService.GetVentasPorProductoAsync(finalStartDate, finalEndDate);
                return Results.Ok(result);
            })
            .WithName("GetSalesByProductReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.VentasPorProductoDto>));

            reportGroup.MapGet("/sales/customer-activity", async (IReportService reportService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();

                var result = await reportService.GetActividadClientesAsync(finalStartDate, finalEndDate);
                return Results.Ok(result);
            })
            .WithName("GetCustomerActivityReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.ClienteActividadDto>));

            reportGroup.MapGet("/sales/accounts-receivable", async (IReportService reportService) =>
            {
                var result = await reportService.GetCuentasPorCobrarAsync();
                return Results.Ok(result);
            })
            .WithName("GetAccountsReceivableReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.CuentasPorCobrarDto>));

            reportGroup.MapGet("/sales/credit-notes", async (IReportService reportService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();

                var result = await reportService.GetNotasDeCreditoAsync(finalStartDate, finalEndDate);
                return Results.Ok(result);
            })
            .WithName("GetCreditNotesReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.NotasDeCreditoReportDto>));

            reportGroup.MapGet("/sales/by-period/pdf", async (IReportService reportService, ReportPdfGeneratorService pdfService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();

                var reportData = await reportService.GetVentasPorPeriodoAsync(finalStartDate, finalEndDate);

                if (reportData == null || !reportData.Any())
                {
                    return Results.NotFound("No se encontraron datos para generar el PDF.");
                }

                var pdfBytes = pdfService.GenerateVentasPorPeriodoPdf(reportData, finalStartDate, finalEndDate);
                return Results.File(pdfBytes, "application/pdf", $"Reporte_Ventas_Periodo_{finalStartDate.ToString("yyyyMMdd")}-{finalEndDate.ToString("yyyyMMdd")}.pdf");
            })
            .WithName("GetSalesByPeriodPdf")
            .Produces(200, typeof(byte[]));

            reportGroup.MapGet("/sales/by-product/pdf", async (IReportService reportService, ReportPdfGeneratorService pdfService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();
                var reportData = await reportService.GetVentasPorProductoAsync(finalStartDate, finalEndDate);
                if (reportData == null || !reportData.Any()) return Results.NotFound("No se encontraron datos para generar el PDF.");
                var pdfBytes = pdfService.GenerateVentasPorProductoPdf(reportData, finalStartDate, finalEndDate);
                return Results.File(pdfBytes, "application/pdf", $"Reporte_Ventas_Producto_{finalStartDate:yyyyMMdd}-{finalEndDate:yyyyMMdd}.pdf");
            })
            .WithName("GetSalesByProductPdf")
            .Produces(200, typeof(byte[]));

            reportGroup.MapGet("/sales/customer-activity/pdf", async (IReportService reportService, ReportPdfGeneratorService pdfService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();
                var reportData = await reportService.GetActividadClientesAsync(finalStartDate, finalEndDate);
                if (reportData == null || !reportData.Any()) return Results.NotFound("No se encontraron datos para generar el PDF.");
                var pdfBytes = pdfService.GenerateActividadClientesPdf(reportData, finalStartDate, finalEndDate);
                return Results.File(pdfBytes, "application/pdf", $"Reporte_Actividad_Clientes_{finalStartDate:yyyyMMdd}-{finalEndDate:yyyyMMdd}.pdf");
            })
            .WithName("GetCustomerActivityPdf")
            .Produces(200, typeof(byte[]));

            reportGroup.MapGet("/sales/accounts-receivable/pdf", async (IReportService reportService, ReportPdfGeneratorService pdfService) =>
            {
                var reportData = await reportService.GetCuentasPorCobrarAsync();
                if (reportData == null || !reportData.Any()) return Results.NotFound("No se encontraron datos para generar el PDF.");
                var pdfBytes = pdfService.GenerateCuentasPorCobrarPdf(reportData);
                return Results.File(pdfBytes, "application/pdf", $"Reporte_Cuentas_Por_Cobrar_{DateTime.Now:yyyyMMdd}.pdf");
            })
            .WithName("GetAccountsReceivablePdf")
            .Produces(200, typeof(byte[]));

            reportGroup.MapGet("/sales/credit-notes/pdf", async (IReportService reportService, ReportPdfGeneratorService pdfService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();
                var reportData = await reportService.GetNotasDeCreditoAsync(finalStartDate, finalEndDate);
                if (reportData == null || !reportData.Any()) return Results.NotFound("No se encontraron datos para generar el PDF.");
                var pdfBytes = pdfService.GenerateNotasDeCreditoPdf(reportData, finalStartDate, finalEndDate);
                return Results.File(pdfBytes, "application/pdf", $"Reporte_Notas_De_Credito_{finalStartDate:yyyyMMdd}-{finalEndDate:yyyyMMdd}.pdf");
            })
            .WithName("GetCreditNotesPdf")
            .Produces(200, typeof(byte[]));

            reportGroup.MapGet("/warehouse/current-stock", async (IReportService reportService) =>
            {
                var result = await reportService.GetStockActualAsync();
                return Results.Ok(result);
            })
            .WithName("GetCurrentStockReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.StockActualDto>));

            reportGroup.MapGet("/warehouse/inventory-movements", async (IReportService reportService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();
                var result = await reportService.GetMovimientosInventarioAsync(finalStartDate, finalEndDate);
                return Results.Ok(result);
            })
            .WithName("GetInventoryMovementsReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.MovimientoInventarioDto>));

            reportGroup.MapGet("/warehouse/purchases-by-period", async (IReportService reportService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();
                var result = await reportService.GetComprasPorPeriodoAsync(finalStartDate, finalEndDate);
                return Results.Ok(result);
            })
            .WithName("GetPurchasesByPeriodReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.ComprasPorPeriodoDto>));

            reportGroup.MapGet("/warehouse/low-stock-products", async (IReportService reportService) =>
            {
                var result = await reportService.GetProductosBajoStockMinimoAsync();
                return Results.Ok(result);
            })
            .WithName("GetLowStockProductsReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.ProductoStockMinimoDto>));

            reportGroup.MapGet("/warehouse/inventory-adjustments", async (IReportService reportService, DateTime? startDate, DateTime? endDate) =>
            {
                var finalStartDate = (startDate ?? DateTime.Now.AddMonths(-1)).ToUniversalTime();
                var finalEndDate = (endDate ?? DateTime.Now).ToUniversalTime();
                var result = await reportService.GetAjustesInventarioAsync(finalStartDate, finalEndDate);
                return Results.Ok(result);
            })
            .WithName("GetInventoryAdjustmentsReport")
            .Produces(200, typeof(IEnumerable<FacturasSRI.Application.Dtos.Reports.AjusteInventarioReportDto>));
        }
    }
}
