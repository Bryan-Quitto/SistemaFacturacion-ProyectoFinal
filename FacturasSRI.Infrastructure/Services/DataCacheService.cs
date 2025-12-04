using FacturasSRI.Application.Dtos;
using FacturasSRI.Domain.Entities;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class DataCacheService : IHostedService, IDisposable
    {
        // Usamos la fábrica de contexto directamente para no depender de otros Servicios
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;
        private Timer? _timer;

        public DataCacheService(IDbContextFactory<FacturasSRIDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Mantenemos el timer como respaldo por si algo falla, cada 10 min es suficiente
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            try 
            {
                await GenerateProductCache();
                await GenerateCustomerCache();
            }
            catch
            {
                // Ignorar errores en el background task para no tumbar la app
            }
        }

        public async Task GenerateProductCache()
        {
            // Creamos un contexto ligero y efímero solo para leer y volcar a JSON
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            // Proyección directa a DTO (optimizada)
            var products = await context.Productos
                .AsNoTracking() // Importante para rendimiento
                .Include(p => p.Lotes)
                .Where(p => p.EstaActivo) // Solo activos
                .OrderBy(p => p.Nombre)
                .Select(p => new ProductDto 
                { 
                    Id = p.Id, 
                    Nombre = p.Nombre, 
                    CodigoPrincipal = p.CodigoPrincipal, 
                    PrecioVentaUnitario = p.PrecioVentaUnitario, 
                    IsActive = p.EstaActivo, 
                    ManejaInventario = p.ManejaInventario, 
                    ManejaLotes = p.ManejaLotes,
                    // Calculamos el stock total directamente en la consulta
                    StockTotal = p.ManejaLotes ? p.Lotes.Sum(l => l.CantidadDisponible) : p.StockTotal, 
                    StockMinimo = p.StockMinimo,
                    PrecioCompraPromedioPonderado = p.PrecioCompraPromedioPonderado 
                })
                .ToListAsync();

            var json = JsonSerializer.Serialize(products);
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cache", "products.json");
            
            // Nos aseguramos que el directorio exista
            var directory = Path.GetDirectoryName(path);
            if(directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, json);
        }

        public async Task GenerateCustomerCache()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Proyección directa a DTO (optimizada)
            var customers = await context.Clientes
                .AsNoTracking()
                .Where(c => c.EstaActivo)
                .Select(c => new CustomerDto
                {
                    Id = c.Id,
                    TipoIdentificacion = c.TipoIdentificacion,
                    NumeroIdentificacion = c.NumeroIdentificacion,
                    RazonSocial = c.RazonSocial,
                    Email = c.Email,
                    Direccion = c.Direccion,
                    Telefono = c.Telefono,
                    EstaActivo = c.EstaActivo
                })
                .ToListAsync();

            var json = JsonSerializer.Serialize(customers);
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cache", "customers.json");
            
            var directory = Path.GetDirectoryName(path);
            if(directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, json);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}