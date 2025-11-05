using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace FacturasSRI.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetHealth()
        {
            return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
        }
    }
}