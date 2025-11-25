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
    public class RoleService : IRoleService
    {
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;

        public RoleService(IDbContextFactory<FacturasSRIDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<RoleDto> CreateRoleAsync(RoleDto roleDto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var role = new Rol
            {
                Id = Guid.NewGuid(),
                Nombre = roleDto.Nombre,
                Descripcion = roleDto.Descripcion,
                EstaActivo = roleDto.EstaActivo
            };
            context.Roles.Add(role);
            await context.SaveChangesAsync();
            roleDto.Id = role.Id;
            return roleDto;
        }

        public async Task DeleteRoleAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var role = await context.Roles.FindAsync(id);
            if (role != null)
            {
                role.EstaActivo = false;
                await context.SaveChangesAsync();
            }
        }

        public async Task<RoleDto?> GetRoleByIdAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var role = await context.Roles.FindAsync(id);
            if (role == null || !role.EstaActivo)
            {
                return null;
            }
            return new RoleDto
            {
                Id = role.Id,
                Nombre = role.Nombre,
                Descripcion = role.Descripcion,
                EstaActivo = role.EstaActivo
            };
        }

        public async Task<List<RoleDto>> GetRolesAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Roles.Where(r => r.EstaActivo).Select(role => new RoleDto
            {
                Id = role.Id,
                Nombre = role.Nombre,
                Descripcion = role.Descripcion,
                EstaActivo = role.EstaActivo
            }).ToListAsync();
        }

        public async Task UpdateRoleAsync(RoleDto roleDto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var role = await context.Roles.FindAsync(roleDto.Id);
            if (role != null)
            {
                role.Nombre = roleDto.Nombre;
                role.Descripcion = roleDto.Descripcion;
                role.EstaActivo = roleDto.EstaActivo;
                await context.SaveChangesAsync();
            }
        }
    }
}