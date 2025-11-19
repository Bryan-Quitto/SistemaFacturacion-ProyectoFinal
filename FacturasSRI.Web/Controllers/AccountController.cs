using FacturasSRI.Application.Dtos;
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

        public AccountController(FacturasSRIDbContext context, ILogger<AccountController> logger, IMemoryCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Login([FromForm] LoginRequestDto loginRequest)
        {
            var returnUrl = loginRequest.ReturnUrl ?? "/dashboard";

            var user = await _context.Usuarios
                .Include(u => u.UsuarioRoles)
                .ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(u => u.Email == loginRequest.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash) || !user.EstaActivo)
            {
                var error = Uri.EscapeDataString("Credenciales inválidas o cuenta inactiva.");
                return Redirect($"/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}&error={error}");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.PrimerNombre),
                new Claim(ClaimTypes.Email, user.Email),
            };

            foreach (var usuarioRol in user.UsuarioRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, usuarioRol.Rol.Nombre));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
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
    }
}