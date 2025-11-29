using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FacturasSRI.Web.Controllers
{
    [ApiController]
    [Route("api/cobros")]
    public class CobrosController : ControllerBase
    {
        private readonly ICobroService _cobroService;
        private readonly ILogger<CobrosController> _logger;

        public CobrosController(ICobroService cobroService, ILogger<CobrosController> logger)
        {
            _cobroService = cobroService;
            _logger = logger;
        }

        [HttpGet("cliente")]
        [Authorize(AuthenticationSchemes = "CustomerAuth", Policy = "IsCustomer")]
        public async Task<ActionResult<PaginatedList<CobroDto>>> GetClientCobros(
            [FromQuery] int pageNumber = 1, 
            [FromQuery] int pageSize = 10, 
            [FromQuery] string? searchTerm = null)
        {
            _logger.LogWarning("[API /api/cobros/cliente] Request received.");
            if (User.Identity?.IsAuthenticated ?? false)
            {
                _logger.LogInformation("[API] User is AUTHENTICATED.");
            }
            else
            {
                _logger.LogError("[API] User is NOT AUTHENTICATED.");
            }

            var clienteIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (clienteIdClaim == null || !Guid.TryParse(clienteIdClaim, out var clienteId))
            {
                _logger.LogError("[API] Could not find or parse ClienteId claim.");
                return Unauthorized("No se pudo identificar al cliente.");
            }

            var cobros = await _cobroService.GetCobrosByClientIdAsync(clienteId, pageNumber, pageSize, searchTerm);
            return Ok(cobros);
        }
    }
}
