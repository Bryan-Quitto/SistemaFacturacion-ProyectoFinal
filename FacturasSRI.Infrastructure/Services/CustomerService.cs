using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Domain.Enums;
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
    public class CustomerService : ICustomerService
    {
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;
        private readonly IValidationService _validationService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly DataCacheService _dataCacheService;

        public CustomerService(
            IDbContextFactory<FacturasSRIDbContext> contextFactory, 
            IValidationService validationService,
            IEmailService emailService,
            IConfiguration configuration,
            DataCacheService dataCacheService)
        {
            _contextFactory = contextFactory;
            _validationService = validationService;
            _emailService = emailService;
            _configuration = configuration;
            _dataCacheService = dataCacheService;
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

            // Regenerate cache
            await _dataCacheService.GenerateCustomerCache();

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
                
                // Regenerate cache
                await _dataCacheService.GenerateCustomerCache();
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

        public async Task<PaginatedList<CustomerDto>> GetCustomersAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActive, TipoIdentificacion? tipoIdentificacion)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = from customer in context.Clientes
                          join usuarioCreador in context.Usuarios on customer.UsuarioIdCreador equals usuarioCreador.Id into usuarioCreadorJoin
                          from usuarioCreador in usuarioCreadorJoin.DefaultIfEmpty()
                          join usuarioModificador in context.Usuarios on customer.UsuarioModificadorId equals usuarioModificador.Id into usuarioModificadorJoin
                          from usuarioModificador in usuarioModificadorJoin.DefaultIfEmpty()
                          select new
                          {
                              customer,
                              usuarioCreador,
                              usuarioModificador
                          };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(x => 
                    x.customer.RazonSocial.Contains(searchTerm) || 
                    x.customer.NumeroIdentificacion.Contains(searchTerm) ||
                    (x.customer.Email != null && x.customer.Email.Contains(searchTerm)) ||
                    (x.customer.Telefono != null && x.customer.Telefono.Contains(searchTerm)));
            }

            if (isActive.HasValue)
            {
                query = query.Where(x => x.customer.EstaActivo == isActive.Value);
            }

            if (tipoIdentificacion.HasValue)
            {
                query = query.Where(x => x.customer.TipoIdentificacion == tipoIdentificacion.Value);
            }

            var finalQuery = query
                .OrderByDescending(x => x.customer.FechaCreacion)
                .Select(x => new CustomerDto
                {
                    Id = x.customer.Id,
                    TipoIdentificacion = x.customer.TipoIdentificacion,
                    NumeroIdentificacion = x.customer.NumeroIdentificacion,
                    RazonSocial = x.customer.RazonSocial,
                    Email = x.customer.Email,
                    Direccion = x.customer.Direccion,
                    Telefono = x.customer.Telefono,
                    EstaActivo = x.customer.EstaActivo,
                    CreadoPor = x.usuarioCreador != null ? x.usuarioCreador.PrimerNombre + " " + x.usuarioCreador.PrimerApellido : "Usuario no encontrado",
                    FechaCreacion = x.customer.FechaCreacion,
                    FechaModificacion = x.customer.FechaModificacion,
                    UltimaModificacionPor = x.usuarioModificador != null ? x.usuarioModificador.PrimerNombre + " " + x.usuarioModificador.PrimerApellido : "N/A"
                });

            return await PaginatedList<CustomerDto>.CreateAsync(finalQuery, pageNumber, pageSize);
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

                // Regenerate cache
                await _dataCacheService.GenerateCustomerCache();
            }
        }

        public async Task<CustomerDto> RegistrarNuevoClienteAsync(ClienteRegistroDto dto)
        {
            if (!_validationService.IsValid(dto.NumeroIdentificacion, dto.TipoIdentificacion.ToString()))
            {
                throw new InvalidOperationException("El número de identificación no es válido.");
            }

            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingCustomer = await context.Clientes
                .FirstOrDefaultAsync(c => c.NumeroIdentificacion == dto.NumeroIdentificacion || c.Email == dto.Email);

            if (existingCustomer != null)
            {
                throw new InvalidOperationException("Ya existe un cliente con el mismo número de identificación o correo electrónico.");
            }

            var confirmationToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var customer = new Cliente
            {
                Id = Guid.NewGuid(),
                TipoIdentificacion = dto.TipoIdentificacion,
                NumeroIdentificacion = dto.NumeroIdentificacion,
                RazonSocial = dto.RazonSocial,
                Email = dto.Email,
                Direccion = dto.Direccion,
                Telefono = dto.Telefono,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FechaCreacion = DateTime.UtcNow,
                EstaActivo = false, // The account is inactive until confirmed
                IsEmailConfirmed = false,
                EmailConfirmationToken = confirmationToken
            };

            context.Clientes.Add(customer);
            await context.SaveChangesAsync();

            var baseUrl = _configuration["App:BaseUrl"] ?? "https://localhost:7123";
            var confirmationLink = $"{baseUrl}/api/customer-registration/confirm-email?token={Uri.EscapeDataString(confirmationToken)}";
            await _emailService.SendCustomerConfirmationEmailAsync(customer.Email, customer.RazonSocial, confirmationLink);

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

        public async Task<CustomerDto?> AutenticarClienteAsync(ClienteLoginDto dto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var customer = await context.Clientes.FirstOrDefaultAsync(c => c.Email == dto.Email);

            if (customer == null || !customer.EstaActivo || !customer.IsEmailConfirmed || !BCrypt.Net.BCrypt.Verify(dto.Password, customer.PasswordHash))
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

        public async Task<bool> ConfirmEmailAsync(string token)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var customer = await context.Clientes.FirstOrDefaultAsync(c => c.EmailConfirmationToken == token);

            if (customer == null)
            {
                return false;
            }

            customer.IsEmailConfirmed = true;
            customer.EstaActivo = true;
            customer.EmailConfirmationToken = null; // Token is used, nullify it
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangePasswordAsync(string customerId, ChangePasswordDto passwordDto)
        {
            if (!Guid.TryParse(customerId, out var customerGuid))
            {
                return false;
            }

            await using var context = await _contextFactory.CreateDbContextAsync();
            var customer = await context.Clientes.FindAsync(customerGuid);

            if (customer == null || !BCrypt.Net.BCrypt.Verify(passwordDto.OldPassword, customer.PasswordHash))
            {
                return false;
            }

            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordDto.NewPassword);
            customer.FechaModificacion = DateTime.UtcNow;

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> GeneratePasswordResetTokenAsync(string email)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var customer = await context.Clientes.FirstOrDefaultAsync(c => c.Email == email);
            if (customer == null || !customer.EstaActivo || !customer.IsEmailConfirmed)
            {
                // Don't reveal that the user does not exist or is not active.
                return true;
            }

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            
            customer.PasswordResetToken = BCrypt.Net.BCrypt.HashPassword(token);
            customer.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30);

            await context.SaveChangesAsync();

            var baseUrl = _configuration["App:BaseUrl"] ?? "https://localhost:7123";
            var resetLink = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(token)}";

            // Re-use the existing password reset email service method
            await _emailService.SendPasswordResetEmailAsync(customer.Email, customer.RazonSocial, resetLink);

            return true;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            // This is inefficient, but necessary because we've hashed the token in the DB.
            var customers = await context.Clientes
                .Where(u => u.PasswordResetToken != null && u.PasswordResetTokenExpiry > DateTime.UtcNow)
                .ToListAsync();

            Cliente? customerToUpdate = null;
            foreach (var customer in customers)
            {
                if (BCrypt.Net.BCrypt.Verify(token, customer.PasswordResetToken))
                {
                    customerToUpdate = customer;
                    break;
                }
            }

            if (customerToUpdate == null)
            {
                return false;
            }

            customerToUpdate.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            customerToUpdate.PasswordResetToken = null;
            customerToUpdate.PasswordResetTokenExpiry = null;
            customerToUpdate.FechaModificacion = DateTime.UtcNow;

            await context.SaveChangesAsync();
            return true;
        }
    }
}