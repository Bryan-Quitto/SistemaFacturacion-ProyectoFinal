using FacturasSRI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supabase;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using FacturasSRI.Web.Extensions;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Infrastructure.Services;

namespace FacturasSRI.Web.Endpoints
{
    public static class DownloadEndpoints
    {
        public static void MapDownloadEndpoints(this WebApplication app)
        {
            var downloadsGroup = app.MapGroup("/api/downloads")
                                    .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Cookies" })
                                    .IgnoreAntiforgeryToken();

            var publicGroup = app.MapGroup("/api/public")
                                 .AllowAnonymous()
                                 .IgnoreAntiforgeryToken();

            downloadsGroup.MapGet("/purchase-receipt/{id}", async (
                Guid id,
                [FromQuery] string? type,
                HttpContext httpContext,
                FacturasSRIDbContext dbContext,
                Client supabase,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");
                
                var cuentaPorPagar = await dbContext.CuentasPorPagar.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

                if (cuentaPorPagar == null)
                {
                    return Results.NotFound("El registro de compra no fue encontrado.");
                }

                // LÓGICA MODIFICADA: Usamos un switch para soportar "credit-note"
                string? filePath = type?.ToLower() switch
                {
                    "payment" => cuentaPorPagar.ComprobantePagoPath,
                    "credit-note" => cuentaPorPagar.NotaCreditoPath,
                    "invoice" or _ => cuentaPorPagar.FacturaCompraPath // Por defecto devuelve la factura
                };

                if (string.IsNullOrEmpty(filePath))
                {
                    return Results.NotFound("El archivo solicitado no fue encontrado o no ha sido cargado.");
                }

                var user = httpContext.User;
                var isAdmin = user.IsInRole("Administrador");
                var isBodeguero = user.IsInRole("Bodeguero");

                if (!isAdmin && !isBodeguero)
                {
                    return Results.Forbid();
                }

                try
                {
                    var fileBytes = await supabase.Storage
                        .From("comprobantes-compra")
                        .Download(filePath, null);
                    
                    var fileName = Path.GetFileName(filePath);
                    var contentType = "application/octet-stream";
                    
                    if(fileName.EndsWith(".pdf")) contentType = "application/pdf";
                    if(fileName.EndsWith(".png")) contentType = "image/png";
                    if(fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg")) contentType = "image/jpeg";

                    return Results.File(fileBytes, contentType, fileDownloadName: fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ocurrió un error al intentar descargar el archivo desde Supabase. Path: {Path}", filePath);
                    return Results.StatusCode(500);
                }
            });

            downloadsGroup.MapGet("/invoice-receipt/{id}", async (
                Guid id,
                HttpContext httpContext,
                FacturasSRIDbContext dbContext,
                Client supabase,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");
                
                var cobro = await dbContext.Cobros.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

                if (cobro == null)
                {
                    return Results.NotFound("El registro de cobro no fue encontrado.");
                }

                if (string.IsNullOrEmpty(cobro.ComprobantePagoPath))
                {
                    return Results.NotFound("El archivo solicitado no fue encontrado.");
                }

                var user = httpContext.User;
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = user.IsInRole("Administrador");

                if (cobro.UsuarioIdCreador.ToString() != userId && !isAdmin)
                {
                    return Results.Forbid();
                }

                try
                {
                    var fileBytes = await supabase.Storage
                        .From("comprobantes-facturas-emitidas")
                        .Download(cobro.ComprobantePagoPath, null);
                    
                    var fileName = Path.GetFileName(cobro.ComprobantePagoPath);
                    var contentType = "application/octet-stream";
                    
                    if(fileName.EndsWith(".pdf")) contentType = "application/pdf";
                    if(fileName.EndsWith(".png")) contentType = "image/png";
                    if(fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg")) contentType = "image/jpeg";

                    return Results.File(fileBytes, contentType, fileDownloadName: fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ocurrió un error al intentar descargar el comprobante de cobro desde Supabase. Path: {Path}", cobro.ComprobantePagoPath);
                    return Results.StatusCode(500);
                }
            });

            downloadsGroup.MapGet("/invoice-ride/{id}", async (
                Guid id,
                IInvoiceService invoiceService,
                PdfGeneratorService pdfGenerator,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");

                var factura = await invoiceService.GetInvoiceDetailByIdAsync(id);

                if (factura == null)
                {
                    return Results.NotFound("La factura solicitada no existe.");
                }

                try
                {
                    var pdfBytes = pdfGenerator.GenerarFacturaPdf(factura);
                    var fileName = $"RIDE_{factura.NumeroFactura}.pdf";
                    return Results.File(pdfBytes, "application/pdf", fileDownloadName: fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error generando el RIDE para la factura {Id}", id);
                    return Results.Problem("Ocurrió un error al generar el PDF.");
                }
            });

            downloadsGroup.MapGet("/nc-ride/{id}", async (
                Guid id,
                [FromServices] ICreditNoteService creditNoteService,
                PdfGeneratorService pdfGenerator,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");

                var nc = await creditNoteService.GetCreditNoteDetailByIdAsync(id);

                if (nc == null)
                {
                    return Results.NotFound("La nota de crédito solicitada no existe.");
                }

                try
                {
                    var pdfBytes = pdfGenerator.GenerarNotaCreditoPdf(nc);
                    var fileName = $"RIDE_{nc.NumeroNotaCredito}.pdf";
                    return Results.File(pdfBytes, "application/pdf", fileDownloadName: fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error generando el RIDE para la nota de crédito {Id}", id);
                    return Results.Problem("Ocurrió un error al generar el PDF.");
                }
            });

            publicGroup.MapGet("/invoice-ride/{id}", async (
                Guid id,
                IInvoiceService invoiceService,
                PdfGeneratorService pdfGenerator,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");

                var factura = await invoiceService.GetInvoiceDetailByIdAsync(id);

                if (factura == null)
                {
                    return Results.NotFound("La factura solicitada no existe.");
                }

                try
                {
                    var pdfBytes = pdfGenerator.GenerarFacturaPdf(factura);
                    var fileName = $"RIDE_{factura.NumeroFactura}.pdf";
                    return Results.File(pdfBytes, "application/pdf", fileDownloadName: fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error generando el RIDE público para la factura {Id}", id);
                    return Results.Problem("Ocurrió un error al generar el PDF.");
                }
            });

            publicGroup.MapGet("/nc-ride/{id}", async (
                Guid id,
                [FromServices] ICreditNoteService creditNoteService,
                PdfGeneratorService pdfGenerator,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");

                var nc = await creditNoteService.GetCreditNoteDetailByIdAsync(id);

                if (nc == null)
                {
                    return Results.NotFound("La nota de crédito solicitada no existe.");
                }

                try
                {
                    var pdfBytes = pdfGenerator.GenerarNotaCreditoPdf(nc);
                    var fileName = $"RIDE_{nc.NumeroNotaCredito}.pdf";
                    return Results.File(pdfBytes, "application/pdf", fileDownloadName: fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error generando el RIDE público para la nota de crédito {Id}", id);
                    return Results.Problem("Ocurrió un error al generar el PDF.");
                }
            });
        }
    }
}