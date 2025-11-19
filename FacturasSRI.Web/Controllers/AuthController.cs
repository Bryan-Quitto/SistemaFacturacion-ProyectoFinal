using FacturasSRI.Application.Dtos;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using BCrypt.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace FacturasSRI.Web.Controllers
{
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly FacturasSRIDbContext _context;
        private readonly ILogger<AuthController> _logger;
        private readonly IMemoryCache _cache;

        public AuthController(IConfiguration configuration, FacturasSRIDbContext context, ILogger<AuthController> logger, IMemoryCache cache)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginRequest)
        {
            _logger.LogInformation("Procesando solicitud de login para {Email}", loginRequest.Email);
            
            var lockoutKey = $"lockout_{loginRequest.Email}";
            if (_cache.TryGetValue(lockoutKey, out _))
            {
                return StatusCode(429, "Demasiados intentos fallidos. Por favor, espere 30 segundos.");
            }

            var user = await _context.Usuarios
                .Include(u => u.UsuarioRoles)
                    .ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(u => u.Email == loginRequest.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash))
            {
                HandleFailedLogin(loginRequest.Email);
                if (_cache.TryGetValue(lockoutKey, out _))
                {
                    return StatusCode(429, "Demasiados intentos fallidos. Por favor, espere 30 segundos.");
                }
                return Unauthorized("Credenciales inválidas.");
            }

            if (!user.EstaActivo)
            {
                _logger.LogWarning("Intento de inicio de sesión de usuario inactivo: {Email}", loginRequest.Email);
                return Unauthorized("Su cuenta ha sido desactivada. Por favor, contacte al administrador.");
            }
            
            var failureCountKey = $"failures_{loginRequest.Email}";
            _cache.Remove(failureCountKey);

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

            _logger.LogInformation("Llamando a HttpContext.SignInAsync para crear la cookie...");
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
            _logger.LogInformation("Llamada a SignInAsync completada.");
            
            var cookiesHeader = HttpContext.Response.Headers["Set-Cookie"].ToString();
            _logger.LogInformation("Encabezado Set-Cookie en la respuesta del servidor: {CookieHeader}", string.IsNullOrEmpty(cookiesHeader) ? "¡¡¡NO ENCONTRADO!!!" : cookiesHeader);

            _logger.LogInformation("Generando token JWT...");
            var token = GenerateJwtToken(user);
            _logger.LogInformation("Login exitoso. Devolviendo token.");
            
            return Ok(new { token });
        }
        
        private void HandleFailedLogin(string email)
        {
            var failureCountKey = $"failures_{email}";
            var failureCount = _cache.GetOrCreate(failureCountKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                return 0;
            });

            failureCount++;
            _cache.Set(failureCountKey, failureCount);

            if (failureCount >= 3)
            {
                var lockoutKey = $"lockout_{email}";
                _cache.Set(lockoutKey, true, TimeSpan.FromSeconds(30));
                _cache.Remove(failureCountKey);
            }
        }

        private string GenerateJwtToken(Usuario user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.PrimerNombre),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            
            foreach (var usuarioRol in user.UsuarioRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, usuarioRol.Rol.Nombre));
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(120),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}