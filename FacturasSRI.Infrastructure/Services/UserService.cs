using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly FacturasSRIDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public UserService(FacturasSRIDbContext context, IEmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
        }

        private string GenerateTemporaryPassword(int length = 12)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()";
            var random = new Random();
            var password = new char[length];
            for (int i = 0; i < length; i++)
            {
                password[i] = validChars[random.Next(validChars.Length)];
            }
            return new string(password);
        }

        public async Task<bool> GeneratePasswordResetTokenAsync(string email)
        {
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                // No revelar si el usuario existe o no por seguridad.
                return true;
            }

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            
            // Guardar el hash del token en la BD, no el token en texto plano.
            user.PasswordResetToken = BCrypt.Net.BCrypt.HashPassword(token);
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30);

            await _context.SaveChangesAsync();

            // La URL base de la aplicación debería estar en appsettings.json
            var baseUrl = _configuration["App:BaseUrl"] ?? "https://localhost:7123";
            var resetLink = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(token)}";

            await _emailService.SendPasswordResetEmailAsync(user.Email, user.PrimerNombre, resetLink);

            return true;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var users = await _context.Usuarios
                .Where(u => u.PasswordResetToken != null && u.PasswordResetTokenExpiry > DateTime.UtcNow)
                .ToListAsync();

            Usuario? userToUpdate = null;
            foreach (var user in users)
            {
                if (BCrypt.Net.BCrypt.Verify(token, user.PasswordResetToken))
                {
                    userToUpdate = user;
                    break;
                }
            }

            if (userToUpdate == null)
            {
                return false;
            }

            userToUpdate.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            userToUpdate.PasswordResetToken = null;
            userToUpdate.PasswordResetTokenExpiry = null;
            userToUpdate.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        
        public async Task<UserDto> CreateUserAsync(UserDto userDto)
        {
            var temporaryPassword = GenerateTemporaryPassword();

            var user = new Usuario
            {
                Id = Guid.NewGuid(),
                PrimerNombre = userDto.PrimerNombre,
                SegundoNombre = userDto.SegundoNombre,
                PrimerApellido = userDto.PrimerApellido,
                SegundoApellido = userDto.SegundoApellido,
                Email = userDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
                EstaActivo = userDto.EstaActivo
            };

            foreach (var rolId in userDto.RolesId)
            {
                user.UsuarioRoles.Add(new UsuarioRol { RolId = rolId });
            }

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            await _emailService.SendWelcomeEmailAsync(user.Email, user.PrimerNombre, temporaryPassword);
            
            userDto.Id = user.Id;
            return userDto;
        }

        public async Task DeleteUserAsync(Guid id)
        {
            var user = await _context.Usuarios.FindAsync(id);
            if (user != null)
            {
                user.EstaActivo = !user.EstaActivo; // Toggle the active status
                await _context.SaveChangesAsync();
            }
        }

        public async Task<UserDto?> GetUserByIdAsync(Guid id)
        {
            var user = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.UsuarioRoles)
                    .ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null || !user.EstaActivo)
            {
                return null;
            }

            return new UserDto
            {
                Id = user.Id,
                PrimerNombre = user.PrimerNombre,
                SegundoNombre = user.SegundoNombre,
                PrimerApellido = user.PrimerApellido,
                SegundoApellido = user.SegundoApellido,
                Email = user.Email,
                EstaActivo = user.EstaActivo,
                RolesId = user.UsuarioRoles.Select(ur => ur.RolId).ToList(),
                Roles = user.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
            };
        }

        public async Task<List<UserDto>> GetUsersAsync()
        {
            return await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.UsuarioRoles)
                    .ThenInclude(ur => ur.Rol)
                .Select(user => new UserDto
                {
                    Id = user.Id,
                    PrimerNombre = user.PrimerNombre,
                    SegundoNombre = user.SegundoNombre,
                    PrimerApellido = user.PrimerApellido,
                    SegundoApellido = user.SegundoApellido,
                    Email = user.Email,
                    EstaActivo = user.EstaActivo,
                    Roles = user.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList(),
                    RolesId = user.UsuarioRoles.Select(ur => ur.RolId).ToList()
                }).ToListAsync();
        }

        public async Task<List<UserDto>> GetActiveUsersAsync()
        {
            return await _context.Usuarios
                .Where(u => u.EstaActivo)
                .AsNoTracking()
                .Include(u => u.UsuarioRoles)
                    .ThenInclude(ur => ur.Rol)
                .Select(user => new UserDto
                {
                    Id = user.Id,
                    PrimerNombre = user.PrimerNombre,
                    SegundoNombre = user.SegundoNombre,
                    PrimerApellido = user.PrimerApellido,
                    SegundoApellido = user.SegundoApellido,
                    Email = user.Email,
                    EstaActivo = user.EstaActivo,
                    Roles = user.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList(),
                    RolesId = user.UsuarioRoles.Select(ur => ur.RolId).ToList()
                }).ToListAsync();
        }

        public async Task UpdateUserAsync(UserDto userDto)
        {
            var user = await _context.Usuarios
                .Include(u => u.UsuarioRoles)
                .FirstOrDefaultAsync(u => u.Id == userDto.Id);

            if (user != null)
            {
                user.PrimerNombre = userDto.PrimerNombre;
                user.SegundoNombre = userDto.SegundoNombre;
                user.PrimerApellido = userDto.PrimerApellido;
                user.SegundoApellido = userDto.SegundoApellido;
                user.Email = userDto.Email;
                user.EstaActivo = userDto.EstaActivo;

                _context.UsuarioRoles.RemoveRange(user.UsuarioRoles);
                foreach (var rolId in userDto.RolesId)
                {
                    user.UsuarioRoles.Add(new UsuarioRol { RolId = rolId });
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task<UserDto?> GetUserProfileAsync(string userId)
        {
            if (!Guid.TryParse(userId, out var userGuid))
            {
                return null;
            }
            return await GetUserByIdAsync(userGuid);
        }

        public async Task UpdateUserProfileAsync(string userId, UpdateProfileDto profileDto)
        {
            if (!Guid.TryParse(userId, out var userGuid))
            {
                return;
            }

            var user = await _context.Usuarios.FindAsync(userGuid);

            if (user != null)
            {
                user.PrimerNombre = profileDto.PrimerNombre;
                user.SegundoNombre = profileDto.SegundoNombre;
                user.PrimerApellido = profileDto.PrimerApellido;
                user.SegundoApellido = profileDto.SegundoApellido;
                user.FechaModificacion = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ChangePasswordAsync(string userId, ChangePasswordDto passwordDto)
        {
            if (!Guid.TryParse(userId, out var userGuid))
            {
                return false;
            }

            var user = await _context.Usuarios.FindAsync(userGuid);

            if (user == null || !BCrypt.Net.BCrypt.Verify(passwordDto.OldPassword, user.PasswordHash))
            {
                return false;
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordDto.NewPassword);
            user.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}


                