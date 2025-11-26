using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FacturasSRI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "VendedorPolicy")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        // [HttpGet]
        // public async Task<ActionResult<List<InvoiceDto>>> GetInvoices()
        // {
        //     return Ok(await _invoiceService.GetInvoicesAsync());
        // }

        [HttpPost]
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
        public async Task<ActionResult<InvoiceDto>> GetInvoice(Guid id)
        {
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }
            return Ok(invoice);
        }
    }
}