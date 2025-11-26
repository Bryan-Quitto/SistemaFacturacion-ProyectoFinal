using FacturasSRI.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class DataCacheService : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer? _timer;

        public DataCacheService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            await GenerateProductCache();
            await GenerateCustomerCache();
        }

        public async Task GenerateProductCache()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
                var products = await productService.GetAllProductsForCacheAsync();
                var json = JsonSerializer.Serialize(products);
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cache", "products.json");
                var directory = Path.GetDirectoryName(path);
                if(directory != null)
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(path, json);
            }
        }

        public async Task GenerateCustomerCache()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
                var customers = await customerService.GetActiveCustomersAsync();
                var json = JsonSerializer.Serialize(customers);
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cache", "customers.json");
                var directory = Path.GetDirectoryName(path);
                if(directory != null)
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(path, json);
            }
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
