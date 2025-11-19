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
                HttpContext httpContext,
                FacturasSRIDbContext dbContext,
                Client supabase,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("DownloadEndpoints");
                logger.LogInformation("Descarga de comprobante solicitada. ID: {Id}", id);
                
                var cuentaPorPagar = await dbContext.CuentasPorPagar.FirstOrDefaultAsync(c => c.Id == id);

                if (cuentaPorPagar == null || string.IsNullOrEmpty(cuentaPorPagar.ComprobantePath))
                {
                    logger.LogWarning("No se encontró la cuenta por pagar o no tiene comprobante. ID: {Id}", id);
                    return Results.NotFound("El comprobante no fue encontrado.");
                }

                var user = httpContext.User;
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = user.IsInRole("Administrador");

                if (cuentaPorPagar.UsuarioIdCreador.ToString() != userId && !isAdmin)
                {
                    logger.LogWarning("Acceso denegado para descargar el comprobante. Usuario: {UserId}, Creador: {CreatorId}", userId, cuentaPorPagar.UsuarioIdCreador);
                    return Results.Forbid();
                }

                try
                {
                    logger.LogInformation("Descargando archivo desde Supabase: {Path}", cuentaPorPagar.ComprobantePath);
                    var fileBytes = await supabase.Storage
                        .From("comprobantes-compra")
                        .Download(cuentaPorPagar.ComprobantePath, null);
                    
                    var fileName = Path.GetFileName(cuentaPorPagar.ComprobantePath);

                    return Results.File(fileBytes, "application/pdf", fileDownloadName: fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ocurrió un error al intentar descargar el archivo desde Supabase. Path: {Path}", cuentaPorPagar.ComprobantePath);
                    return Results.StatusCode(500);
                }
            });
        }
    }
}