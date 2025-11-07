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

namespace FacturasSRI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly FacturasSRIDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IConfiguration configuration, FacturasSRIDbContext context, ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginRequest)
        {
            _logger.LogInformation("--- Inicio de Intento de Login ---");
            _logger.LogInformation($"Buscando usuario por email: {loginRequest.Email}");

            var user = await _context.Usuarios
                .Include(u => u.UsuarioRoles)
                    .ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(u => u.Email == loginRequest.Email);

            if (user == null)
            {
                _logger.LogWarning($"Resultado: Usuario NO ENCONTRADO con email: {loginRequest.Email}");
                _logger.LogInformation("--- Fin de Intento de Login ---");
                return Unauthorized();
            }
            
            _logger.LogInformation($"Resultado: Usuario ENCONTRADO. ID: {user.Id}");
            _logger.LogInformation($"Verificando contraseña contra el hash de la BD...");

            bool isPasswordCorrect = false;
            try
            {
                isPasswordCorrect = BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BCrypt.Verify lanzó una excepción. El hash en la BD podría estar corrupto o no ser un hash de BCrypt.");
                _logger.LogInformation("--- Fin de Intento de Login ---");
                return Unauthorized();
            }

            if (!isPasswordCorrect)
            {
                _logger.LogWarning("Resultado: Contraseña INCORRECTA.");
                _logger.LogInformation("--- Fin de Intento de Login ---");
                return Unauthorized();
            }

            _logger.LogInformation("Resultado: Contraseña CORRECTA. Generando sesión y token.");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
            };

            foreach (var usuarioRol in user.UsuarioRoles)
            {
                _logger.LogInformation($"Añadiendo Rol al claim: {usuarioRol.Rol.Nombre}");
                claims.Add(new Claim(ClaimTypes.Role, usuarioRol.Rol.Nombre));
            }

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            var token = GenerateJwtToken(user, claims);
            
            _logger.LogInformation("--- Fin de Intento de Login (Éxito) ---");
            return Ok(new { token });
        }

        private string GenerateJwtToken(Usuario user, IEnumerable<Claim> claims)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claimsList = new List<Claim>(claims);
            if (!claimsList.Exists(c => c.Type == JwtRegisteredClaimNames.Jti))
            {
                claimsList.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claimsList,
                expires: DateTime.Now.AddMinutes(120),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}