using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly FacturasSRIDbContext _context;
        public UserService(FacturasSRIDbContext context)
        {
            _context = context;
        }

        public async Task<UserDto> CreateUserAsync(UserDto userDto)
        {
            if (string.IsNullOrWhiteSpace(userDto.Password))
            {
                throw new ArgumentException("La contraseña es obligatoria para crear un usuario.");
            }

            var user = new Usuario
            {
                Id = Guid.NewGuid(),
                PrimerNombre = userDto.PrimerNombre,
                SegundoNombre = userDto.SegundoNombre,
                PrimerApellido = userDto.PrimerApellido,
                SegundoApellido = userDto.SegundoApellido,
                Email = userDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(userDto.Password),
                EstaActivo = userDto.EstaActivo
            };

            foreach (var rolId in userDto.RolesId)
            {
                user.UsuarioRoles.Add(new UsuarioRol { RolId = rolId });
            }

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();
            
            userDto.Id = user.Id;
            userDto.Password = null;
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
                    Roles = user.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
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
                    Roles = user.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
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

                if (!string.IsNullOrWhiteSpace(userDto.Password))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(userDto.Password);
                }

                _context.UsuarioRoles.RemoveRange(user.UsuarioRoles);
                foreach (var rolId in userDto.RolesId)
                {
                    user.UsuarioRoles.Add(new UsuarioRol { RolId = rolId });
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}