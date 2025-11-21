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

            downloadsGroup.MapGet("/purchase-receipt/{id}", async (
                Guid id,
                [FromQuery] string? type, // "invoice" or "payment"
                HttpContext httpContext,
                FacturasSRIDbContext dbContext,
                Client supabase,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");
                logger.LogInformation("Descarga de comprobante de compra solicitada. ID: {Id}, Tipo: {Type}", id, type);
                
                var cuentaPorPagar = await dbContext.CuentasPorPagar.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

                if (cuentaPorPagar == null)
                {
                    return Results.NotFound("El registro de compra no fue encontrado.");
                }

                string? filePath = (type?.ToLower() == "payment")
                    ? cuentaPorPagar.ComprobantePagoPath
                    : cuentaPorPagar.FacturaCompraPath;

                if (string.IsNullOrEmpty(filePath))
                {
                    logger.LogWarning("No se encontró la ruta del archivo. ID: {Id}, Tipo: {Type}", id, type);
                    return Results.NotFound("El archivo solicitado no fue encontrado.");
                }

                var user = httpContext.User;
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = user.IsInRole("Administrador");

                if (cuentaPorPagar.UsuarioIdCreador.ToString() != userId && !isAdmin)
                {
                    logger.LogWarning("Acceso denegado. Usuario: {UserId}, Creador: {CreatorId}", userId, cuentaPorPagar.UsuarioIdCreador);
                    return Results.Forbid();
                }

                try
                {
                    logger.LogInformation("Descargando archivo desde Supabase: {Path}", filePath);
                    var fileBytes = await supabase.Storage
                        .From("comprobantes-compra")
                        .Download(filePath, null);
                    
                    var fileName = Path.GetFileName(filePath);
                    var contentType = "application/octet-stream"; // Generic default
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

             downloadsGroup.MapGet("/invoice-ride/{id}", async (
                Guid id,
                IInvoiceService invoiceService,
                PdfGeneratorService pdfGenerator,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");
                logger.LogInformation("Generación de RIDE solicitada. InvoiceID: {Id}", id);

                // 1. Obtener los datos de la factura (incluye info del SRI)
                var factura = await invoiceService.GetInvoiceDetailByIdAsync(id);

                if (factura == null)
                {
                    return Results.NotFound("La factura solicitada no existe.");
                }

                try
                {
                    // 2. Generar el PDF en memoria
                    var pdfBytes = pdfGenerator.GenerarFacturaPdf(factura);

                    // 3. Devolver el archivo directamente
                    var fileName = $"RIDE_{factura.NumeroFactura}.pdf";
                    return Results.File(pdfBytes, "application/pdf", fileDownloadName: fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error generando el RIDE para la factura {Id}", id);
                    return Results.Problem("Ocurrió un error al generar el PDF.");
                }
            });
        }
    }
}