using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FacturasSRI.Web.Controllers
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("stats")]
        public async Task<ActionResult<DashboardStatsDto>> GetStats()
        {
            // 1. Obtener ID del usuario
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("No se pudo identificar al usuario.");
            }

            // 2. Obtener Roles
            var isAdmin = User.IsInRole("Administrador");
            var isBodeguero = User.IsInRole("Bodeguero");

            // 3. Llamar al servicio con los parámetros requeridos
            var stats = await _dashboardService.GetDashboardStatsAsync(userId, isAdmin, isBodeguero);
            
            return Ok(stats);
        }
    }
}