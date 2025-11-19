using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Supabase;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IO;

namespace FacturasSRI.Web.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DownloadsController : ControllerBase
    {
        private readonly FacturasSRIDbContext _context;
        private readonly Client _supabase;

        public DownloadsController(FacturasSRIDbContext context, Client supabase)
        {
            _context = context;
            _supabase = supabase;
        }

        [HttpGet("purchase-receipt/{id}")]
        public async Task<IActionResult> DownloadPurchaseReceipt(Guid id)
        {
            var cuentaPorPagar = await _context.CuentasPorPagar.FindAsync(id);

            if (cuentaPorPagar == null || string.IsNullOrEmpty(cuentaPorPagar.ComprobantePath))
            {
                return NotFound("El comprobante no fue encontrado.");
            }

            // Security Check: Only the creator or an Admin can download
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Administrador");

            if (cuentaPorPagar.UsuarioIdCreador.ToString() != userId && !isAdmin)
            {
                return Forbid();
            }

            try
            {
                var fileBytes = await _supabase.Storage
                    .From("comprobantes-compra")
                    .Download(cuentaPorPagar.ComprobantePath, null);
                
                var fileName = Path.GetFileName(cuentaPorPagar.ComprobantePath);

                return File(fileBytes, "application/pdf", fileDownloadName: fileName);
            }
            catch (Exception)
            {
                // TODO: Log the exception
                return StatusCode(500, "Ocurrió un error al intentar descargar el archivo desde el almacenamiento.");
            }
        }
    }
}
