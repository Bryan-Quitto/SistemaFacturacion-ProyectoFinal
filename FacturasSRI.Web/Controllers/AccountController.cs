using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

namespace FacturasSRI.Web.Controllers
{
    [Route("[controller]")]
    public class AccountController : Controller
    {
        private readonly FacturasSRIDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IMemoryCache _cache;
        private readonly ICustomerService _customerService;

        public AccountController(FacturasSRIDbContext context, ILogger<AccountController> logger, IMemoryCache cache, ICustomerService customerService)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _customerService = customerService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Login([FromForm] LoginRequestDto loginRequest)
        {
            var returnUrl = loginRequest.ReturnUrl ?? "/dashboard";

            // 1. Try to authenticate as an internal User (staff)
            var user = await _context.Usuarios
                .Include(u => u.UsuarioRoles)
                .ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(u => u.Email == loginRequest.Email);

            if (user != null && user.EstaActivo && BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash))
            {
                var userClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.PrimerNombre),
                    new Claim(ClaimTypes.Email, user.Email),
                };

                foreach (var usuarioRol in user.UsuarioRoles)
                {
                    userClaims.Add(new Claim(ClaimTypes.Role, usuarioRol.Rol.Nombre));
                }

                var claimsIdentity = new ClaimsIdentity(userClaims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return LocalRedirect(returnUrl);
            }

            // 2. If staff login fails, try to authenticate as a Customer
            var customer = await _customerService.AutenticarClienteAsync(new ClienteLoginDto { Email = loginRequest.Email, Password = loginRequest.Password });
            if (customer != null)
            {
                var customerClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()),
                    new Claim(ClaimTypes.Name, customer.RazonSocial),
                    new Claim(ClaimTypes.Email, customer.Email),
                    new Claim("UserType", "Cliente")
                };

                var customerIdentity = new ClaimsIdentity(customerClaims, "CustomerAuth");
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                };

                await HttpContext.SignInAsync("CustomerAuth", new ClaimsPrincipal(customerIdentity), authProperties);
                
                // Always redirect customers to their portal dashboard
                return LocalRedirect("/cliente/dashboard");
            }

            // 3. If both fail, redirect back to login with an error
            var error = Uri.EscapeDataString("Credenciales inválidas o cuenta inactiva.");
            return Redirect($"/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}&error={error}");
        }

         [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            // This needs to sign out of both schemes
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync("CustomerAuth");
            return Redirect("/");
        }
    }
}