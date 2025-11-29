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
    [Route("api/creditnotes")]
    public class CreditNotesController : ControllerBase
    {
        private readonly ICreditNoteService _creditNoteService;
        private readonly ILogger<CreditNotesController> _logger;

        public CreditNotesController(ICreditNoteService creditNoteService, ILogger<CreditNotesController> logger)
        {
            _creditNoteService = creditNoteService;
            _logger = logger;
        }

        [HttpGet("cliente")]
        [Authorize(AuthenticationSchemes = "CustomerAuth", Policy = "IsCustomer")]
        public async Task<ActionResult<PaginatedList<CreditNoteDto>>> GetClientCreditNotes(
            [FromQuery] int pageNumber = 1, 
            [FromQuery] int pageSize = 10, 
            [FromQuery] string? searchTerm = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            _logger.LogWarning("[API /api/creditnotes/cliente] Request received.");
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

            var creditNotes = await _creditNoteService.GetCreditNotesByClientIdAsync(clienteId, pageNumber, pageSize, searchTerm, startDate, endDate);
            return Ok(creditNotes);
        }

        [HttpGet("cliente/{id:guid}")]
        [Authorize(AuthenticationSchemes = "CustomerAuth", Policy = "IsCustomer")]
        public async Task<ActionResult<CreditNoteDetailViewDto>> GetClientCreditNoteDetail(Guid id)
        {
            var clienteIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (clienteIdClaim == null || !Guid.TryParse(clienteIdClaim, out var clienteId))
            {
                return Unauthorized("No se pudo identificar al cliente.");
            }

            var creditNote = await _creditNoteService.GetCreditNoteDetailByIdAsync(id);

            if (creditNote == null)
            {
                return NotFound();
            }

            if (creditNote.ClienteId != clienteId)
            {
                return Forbid("No tiene permiso para ver esta nota de crédito.");
            }

            return Ok(creditNote);
        }
    }
}
