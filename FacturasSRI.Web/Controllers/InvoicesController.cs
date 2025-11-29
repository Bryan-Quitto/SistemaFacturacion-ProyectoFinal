using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FacturasSRI.Domain.Enums;

namespace FacturasSRI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(IInvoiceService invoiceService, ILogger<InvoicesController> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
        }

        // [HttpGet]
        // public async Task<ActionResult<List<InvoiceDto>>> GetInvoices()
        // {
        //     return Ok(await _invoiceService.GetInvoicesAsync());
        // }

        [HttpPost]
        [Authorize(Policy = "VendedorPolicy")]
        public async Task<ActionResult<InvoiceDto>> CreateInvoice([FromBody] CreateInvoiceDto invoiceDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            try
            {
                var newInvoice = await _invoiceService.CreateInvoiceAsync(invoiceDto);
                return CreatedAtAction(nameof(GetInvoice), new { id = newInvoice.Id }, newInvoice);
            }
            catch (Exception)
            {
                return BadRequest("Error al crear la factura.");
            }
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "VendedorPolicy")]
        public async Task<ActionResult<InvoiceDto>> GetInvoice(Guid id)
        {
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }
            return Ok(invoice);
        }

        [HttpGet("cliente")]
        [Authorize(AuthenticationSchemes = "CustomerAuth", Policy = "IsCustomer")]
        public async Task<ActionResult<PaginatedList<InvoiceDto>>> GetClientInvoices([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] EstadoFactura? status = null, [FromQuery] string? searchTerm = null)
        {
            _logger.LogWarning("[API /api/invoices/cliente] Request received.");
            if (User.Identity?.IsAuthenticated ?? false)
            {
                _logger.LogInformation("[API] User is AUTHENTICATED.");
                foreach (var claim in User.Claims)
                {
                    _logger.LogInformation("[API] Claim -> Type: {Type}, Value: {Value}", claim.Type, claim.Value);
                }
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
            _logger.LogInformation("[API] ClienteId found: {ClienteId}", clienteId);

            var invoices = await _invoiceService.GetInvoicesByClientIdAsync(clienteId, pageNumber, pageSize, status, searchTerm);
            return Ok(invoices);
        }

        [HttpGet("cliente/{id:guid}")]
        [Authorize(AuthenticationSchemes = "CustomerAuth", Policy = "IsCustomer")]
        public async Task<ActionResult<InvoiceDetailViewDto>> GetClientInvoiceDetail(Guid id)
        {
            var clienteIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (clienteIdClaim == null || !Guid.TryParse(clienteIdClaim, out var clienteId))
            {
                return Unauthorized("No se pudo identificar al cliente.");
            }

            var invoice = await _invoiceService.GetInvoiceDetailByIdAsync(id);

            if (invoice == null)
            {
                return NotFound();
            }

            if (invoice.ClienteId != clienteId)
            {
                return Forbid("No tiene permiso para ver esta factura.");
            }

            return Ok(invoice);
        }
    }
}