using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace FacturasSRI.Web.Controllers
{
    [ApiController]
    [Route("api/customer-registration")]
    public class CustomerRegistrationController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomerRegistrationController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Register(ClienteRegistroDto dto)
        {
            try
            {
                var customer = await _customerService.RegistrarNuevoClienteAsync(dto);
                return Ok(customer);
            }
            catch (System.InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return Redirect("/email-confirmation?success=false");
            }

            var result = await _customerService.ConfirmEmailAsync(token);
            
            return Redirect($"/email-confirmation?success={result}");
        }
    }
}
