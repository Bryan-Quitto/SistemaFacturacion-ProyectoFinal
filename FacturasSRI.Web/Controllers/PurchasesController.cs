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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PurchasesController : ControllerBase
    {
        private readonly IPurchaseService _purchaseService;

        public PurchasesController(IPurchaseService purchaseService)
        {
            _purchaseService = purchaseService;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePurchase([FromBody] PurchaseDto purchaseDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var success = await _purchaseService.CreatePurchaseAsync(purchaseDto);

            if (success)
            {
                return Ok();
            }

            return BadRequest("No se pudo registrar la compra.");
        }

        // [HttpGet]
        // public async Task<ActionResult<List<PurchaseListItemDto>>> GetPurchases()
        // {
        //     var purchases = await _purchaseService.GetPurchasesAsync();
        //     return Ok(purchases);
        // }
    }
}