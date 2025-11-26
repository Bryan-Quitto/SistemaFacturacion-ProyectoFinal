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
    public class CustomerService : ICustomerService
    {
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;
        private readonly IValidationService _validationService;

        public CustomerService(IDbContextFactory<FacturasSRIDbContext> contextFactory, IValidationService validationService)
        {
            _contextFactory = contextFactory;
            _validationService = validationService;
        }

        public async Task<CustomerDto> CreateCustomerAsync(CustomerDto customerDto)
        {
            if (!_validationService.IsValid(customerDto.NumeroIdentificacion, customerDto.TipoIdentificacion.ToString()))
            {
                throw new InvalidOperationException("El número de identificación no es válido.");
            }
            
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingCustomer = await context.Clientes
                .FirstOrDefaultAsync(c => c.NumeroIdentificacion == customerDto.NumeroIdentificacion || 
                                           c.RazonSocial == customerDto.RazonSocial || 
                                           c.Telefono == customerDto.Telefono);

            if (existingCustomer != null)
            {
                throw new InvalidOperationException("Ya existe un cliente con el mismo número de identificación, razón social o teléfono.");
            }

            var customer = new Cliente
            {
                Id = Guid.NewGuid(),
                TipoIdentificacion = customerDto.TipoIdentificacion,
                NumeroIdentificacion = customerDto.NumeroIdentificacion,
                RazonSocial = customerDto.RazonSocial,
                Email = customerDto.Email,
                Direccion = customerDto.Direccion,
                Telefono = customerDto.Telefono,
                UsuarioIdCreador = customerDto.UsuarioIdCreador,
                FechaCreacion = DateTime.UtcNow
            };
            context.Clientes.Add(customer);
            await context.SaveChangesAsync();
            customerDto.Id = customer.Id;
            return customerDto;
        }

        public async Task DeleteCustomerAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var customer = await context.Clientes.FindAsync(id);
            if (customer != null)
            {
                customer.EstaActivo = !customer.EstaActivo; // Toggle the active status
                await context.SaveChangesAsync();
            }
        }

        public async Task<CustomerDto?> GetCustomerByIdAsync(Guid id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var customer = await context.Clientes.FindAsync(id);
            if (customer == null || !customer.EstaActivo)
            {
                return null;
            }
            return new CustomerDto
            {
                Id = customer.Id,
                TipoIdentificacion = customer.TipoIdentificacion,
                NumeroIdentificacion = customer.NumeroIdentificacion,
                RazonSocial = customer.RazonSocial,
                Email = customer.Email,
                Direccion = customer.Direccion,
                Telefono = customer.Telefono,
                EstaActivo = customer.EstaActivo
            };
        }

        public async Task<PaginatedList<CustomerDto>> GetCustomersAsync(int pageNumber, int pageSize, string? searchTerm)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = from customer in context.Clientes
                          join usuarioCreador in context.Usuarios on customer.UsuarioIdCreador equals usuarioCreador.Id into usuarioCreadorJoin
                          from usuarioCreador in usuarioCreadorJoin.DefaultIfEmpty()
                          join usuarioModificador in context.Usuarios on customer.UsuarioModificadorId equals usuarioModificador.Id into usuarioModificadorJoin
                          from usuarioModificador in usuarioModificadorJoin.DefaultIfEmpty()
                          select new CustomerDto
                          {
                              Id = customer.Id,
                              TipoIdentificacion = customer.TipoIdentificacion,
                              NumeroIdentificacion = customer.NumeroIdentificacion,
                              RazonSocial = customer.RazonSocial,
                              Email = customer.Email,
                              Direccion = customer.Direccion,
                              Telefono = customer.Telefono,
                              EstaActivo = customer.EstaActivo,
                              CreadoPor = usuarioCreador != null ? usuarioCreador.PrimerNombre + " " + usuarioCreador.PrimerApellido : "Usuario no encontrado",
                              FechaCreacion = customer.FechaCreacion,
                              FechaModificacion = customer.FechaModificacion,
                              UltimaModificacionPor = usuarioModificador != null ? usuarioModificador.PrimerNombre + " " + usuarioModificador.PrimerApellido : "N/A"
                          };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(c => 
                    c.RazonSocial.Contains(searchTerm) || 
                    c.NumeroIdentificacion.Contains(searchTerm) ||
                    (c.Email != null && c.Email.Contains(searchTerm)) ||
                    (c.Telefono != null && c.Telefono.Contains(searchTerm)));
            }
            
            query = query.OrderByDescending(c => c.FechaCreacion);

            return await PaginatedList<CustomerDto>.CreateAsync(query, pageNumber, pageSize);
        }

        public async Task<List<CustomerDto>> GetActiveCustomersAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await (from customer in context.Clientes
                          join usuarioCreador in context.Usuarios on customer.UsuarioIdCreador equals usuarioCreador.Id into usuarioCreadorJoin
                          from usuarioCreador in usuarioCreadorJoin.DefaultIfEmpty()
                          join usuarioModificador in context.Usuarios on customer.UsuarioModificadorId equals usuarioModificador.Id into usuarioModificadorJoin
                          from usuarioModificador in usuarioModificadorJoin.DefaultIfEmpty()
                          where customer.EstaActivo == true // Filter for active customers
                          select new CustomerDto
                          {
                              Id = customer.Id,
                              TipoIdentificacion = customer.TipoIdentificacion,
                              NumeroIdentificacion = customer.NumeroIdentificacion,
                              RazonSocial = customer.RazonSocial,
                              Email = customer.Email,
                              Direccion = customer.Direccion,
                              Telefono = customer.Telefono,
                              EstaActivo = customer.EstaActivo,
                              CreadoPor = usuarioCreador != null ? usuarioCreador.PrimerNombre + " " + usuarioCreador.PrimerApellido : "Usuario no encontrado",
                              FechaCreacion = customer.FechaCreacion,
                              FechaModificacion = customer.FechaModificacion,
                              UltimaModificacionPor = usuarioModificador != null ? usuarioModificador.PrimerNombre + " " + usuarioModificador.PrimerApellido : "N/A"
                          }).ToListAsync();
        }

        public async Task UpdateCustomerAsync(CustomerDto customerDto)
        {
            if (!_validationService.IsValid(customerDto.NumeroIdentificacion, customerDto.TipoIdentificacion.ToString()))
            {
                throw new InvalidOperationException("El número de identificación no es válido.");
            }
            
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingCustomer = await context.Clientes
                .FirstOrDefaultAsync(c => c.Id != customerDto.Id && 
                                           (c.NumeroIdentificacion == customerDto.NumeroIdentificacion || 
                                            c.RazonSocial == customerDto.RazonSocial || 
                                            c.Telefono == customerDto.Telefono));

            if (existingCustomer != null)
            {
                throw new InvalidOperationException("Ya existe otro cliente con el mismo número de identificación, razón social o teléfono.");
            }

            var customer = await context.Clientes.FindAsync(customerDto.Id);
            if (customer != null)
            {
                customer.TipoIdentificacion = customerDto.TipoIdentificacion;
                customer.NumeroIdentificacion = customerDto.NumeroIdentificacion;
                customer.RazonSocial = customerDto.RazonSocial;
                customer.Email = customerDto.Email;
                customer.Direccion = customerDto.Direccion;
                customer.Telefono = customerDto.Telefono;
                customer.EstaActivo = customerDto.EstaActivo; // Update EstaActivo from CustomerDto.EstaActivo
                customer.FechaModificacion = DateTime.UtcNow;
                customer.UsuarioModificadorId = customerDto.UsuarioIdCreador; // Assuming UsuarioIdCreador in DTO is the modifier's ID
                customer.UltimaModificacionPor = customerDto.UltimaModificacionPor; // Assuming UltimaModificacionPor in DTO is the modifier's name
                await context.SaveChangesAsync();
            }
        }
    }
}