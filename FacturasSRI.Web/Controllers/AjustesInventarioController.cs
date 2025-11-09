using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FacturasSRI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Administrador,Bodeguero")]
    public class AjustesInventarioController : ControllerBase
    {
        private readonly IAjusteInventarioService _ajusteInventarioService;
        private readonly ILogger<AjustesInventarioController> _logger;

        public AjustesInventarioController(IAjusteInventarioService ajusteInventarioService, ILogger<AjustesInventarioController> logger)
        {
            _ajusteInventarioService = ajusteInventarioService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<AjusteListItemDto>>> GetAdjustments()
        {
            var adjustments = await _ajusteInventarioService.GetAdjustmentsAsync();
            return Ok(adjustments);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAdjustment([FromBody] AjusteInventarioDto ajusteDto)
        {
            _logger.LogInformation("--- INICIO CreateAdjustment Controller ---");

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("Claim 'NameIdentifier' encontrado: {UserId}", string.IsNullOrEmpty(userIdString) ? "NULO O VACÍO" : userIdString);
            
            var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}").ToList();
            _logger.LogInformation("Todos los claims en el token: {AllClaims}", string.Join(" | ", allClaims));

            if (!Guid.TryParse(userIdString, out var userId))
            {
                _logger.LogWarning("¡FALLO al parsear el Claim a GUID! El valor era: {UserIdString}", userIdString);
                return Unauthorized();
            }
            
            _logger.LogInformation("GUID parseado correctamente: {UserId}", userId);

            ajusteDto.UsuarioIdAutoriza = userId;
            
            try
            {
                await _ajusteInventarioService.CreateAdjustmentAsync(ajusteDto);
                _logger.LogInformation("--- FIN CreateAdjustment Controller (Éxito) ---");
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Operación inválida durante la creación del ajuste.");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción inesperada durante la creación del ajuste.");
                return StatusCode(500, "Ocurrió un error inesperado al procesar el ajuste.");
            }
        }
    }
}