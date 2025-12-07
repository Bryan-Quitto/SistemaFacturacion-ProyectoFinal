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
using FacturasSRI.Application.Dtos;

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

            publicGroup.MapGet("/payment-receipt/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] IDbContextFactory<FacturasSRIDbContext> dbContextFactory,
                PdfGeneratorService pdfGenerator,
                ILoggerFactory loggerFactory) =>
            {
                var user = httpContext.User;
                var isAdmin = user.IsInRole("Administrador");
                var isVendedor = user.IsInRole("Vendedor");
                var isCustomer = user.HasClaim("UserType", "Cliente");
                var clienteIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);

                using var dbContext = await dbContextFactory.CreateDbContextAsync();

                var cobro = await dbContext.Cobros
                    .AsNoTracking()
                    .Include(c => c.Factura)
                    .ThenInclude(f => f.Cliente)
                    .Where(c => c.Id == id)
                    .FirstOrDefaultAsync();

                if (cobro == null) return Results.NotFound("Recibo no encontrado.");

                bool canAccess = false;

                if (isAdmin || isVendedor)
                {
                    canAccess = true;
                }
                else if (isCustomer)
                {
                    if (Guid.TryParse(clienteIdClaim, out var authenticatedCustomerId) && cobro.Factura != null && cobro.Factura.ClienteId.HasValue && cobro.Factura.ClienteId.Value == authenticatedCustomerId)
                    {
                        canAccess = true;
                    }
                }

                if (!canAccess)
                {
                    return Results.Forbid();
                }

                var cobroDto = new CobroDto
                {
                    Id = cobro.Id,
                    FacturaId = cobro.FacturaId,
                    NumeroFactura = cobro.Factura?.NumeroFactura ?? string.Empty,
                    ClienteNombre = cobro.Factura?.Cliente?.RazonSocial ?? string.Empty,
                    FechaCobro = cobro.FechaCobro,
                    Monto = cobro.Monto,
                    MetodoDePago = cobro.MetodoDePago,
                    Referencia = cobro.Referencia
                };

                var pdfBytes = pdfGenerator.GenerarReciboCobroPdf(cobroDto);
                return Results.File(pdfBytes, "application/pdf", $"Recibo_{cobroDto.NumeroFactura}.pdf");
            });

            publicGroup.MapGet("/invoice-receipt/{id}", async (
                Guid id,
                HttpContext httpContext,
                [FromServices] IDbContextFactory<FacturasSRIDbContext> dbContextFactory,
                Client supabase,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");

                await using var dbContext = await dbContextFactory.CreateDbContextAsync();
                var cobro = await dbContext.Cobros
                    .AsNoTracking()
                    .Include(c => c.Factura)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cobro == null)
                {
                    return Results.NotFound("El registro de cobro no fue encontrado.");
                }

                if (string.IsNullOrEmpty(cobro.ComprobantePagoPath))
                {
                    return Results.NotFound("El archivo solicitado no fue encontrado o no ha sido cargado.");
                }

                var user = httpContext.User;
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = user.IsInRole("Administrador");
                var isVendedor = user.IsInRole("Vendedor");

                var isCustomer = user.HasClaim("UserType", "Cliente");
                var clienteIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);

                bool canAccess = false;

                if (isAdmin || isVendedor)
                {
                    canAccess = true;
                }
                else if (isCustomer)
                {
                    if (Guid.TryParse(clienteIdClaim, out var authenticatedCustomerId))
                    {
                        var customerOwnedCobro = await dbContext.Cobros
                            .AsNoTracking()
                            .Include(c => c.Factura)
                            .Where(c => c.Id == id && c.Factura.ClienteId == authenticatedCustomerId)
                            .FirstOrDefaultAsync();

                        if (customerOwnedCobro != null)
                        {
                            canAccess = true;
                        }
                    }
                }
                else if (cobro.UsuarioIdCreador.ToString() == userId)
                {
                    canAccess = true;
                }

                if (!canAccess)
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

                    if (fileName.EndsWith(".pdf")) contentType = "application/pdf";
                    if (fileName.EndsWith(".png")) contentType = "image/png";
                    if (fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg")) contentType = "image/jpeg";

                    return Results.File(fileBytes, contentType, fileDownloadName: fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ocurrió un error al intentar descargar el comprobante de cobro desde Supabase. Path: {Path}", cobro.ComprobantePagoPath);
                    return Results.StatusCode(500);
                }
            });

            downloadsGroup.MapGet("/purchase-receipt/{id}", async (
                Guid id,
                [FromQuery] string? type,
                HttpContext httpContext,
                [FromServices] IDbContextFactory<FacturasSRIDbContext> dbContextFactory,
                Client supabase,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");

                await using var dbContext = await dbContextFactory.CreateDbContextAsync();
                var cuentaPorPagar = await dbContext.CuentasPorPagar.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

                if (cuentaPorPagar == null)
                {
                    return Results.NotFound("El registro de compra no fue encontrado.");
                }

                string? filePath = type?.ToLower() switch
                {
                    "payment" => cuentaPorPagar.ComprobantePagoPath,
                    "credit-note" => cuentaPorPagar.NotaCreditoPath,
                    "invoice" or _ => cuentaPorPagar.FacturaCompraPath
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

                    if (fileName.EndsWith(".pdf")) contentType = "application/pdf";
                    if (fileName.EndsWith(".png")) contentType = "image/png";
                    if (fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg")) contentType = "image/jpeg";

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

            var adminGroup = app.MapGroup("/api/admin")
                                .RequireAuthorization("AdminPolicy")
                                .IgnoreAntiforgeryToken();

            adminGroup.MapPost("/refresh-cache", (
                [FromServices] DataCacheService cacheService,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("AdminEndpoints");
                logger.LogInformation("Solicitud manual para refrescar caché recibida.");
                try
                {
                    // Como estos métodos no son asíncronos (no devuelven Task),
                    // no necesitamos 'await' ni 'async' en la lambda.
                    cacheService.ClearProductsCache();
                    cacheService.ClearCustomersCache();
                    
                    logger.LogInformation("Caché de productos y clientes invalidada exitosamente.");
                    return Results.Ok("Caché de productos y clientes invalidada. Se reconstruirá en la próxima solicitud.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error al regenerar la caché manualmente.");
                    return Results.Problem("Ocurrió un error al refrescar la caché.");
                }
            });
        }
    }
}