using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FacturasSRI.Infrastructure.Services
{
    public class VencimientoComprasService : BackgroundService
    {
        private readonly ILogger<VencimientoComprasService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public VencimientoComprasService(ILogger<VencimientoComprasService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servicio de Vencimiento de Compras iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var purchaseService = scope.ServiceProvider.GetRequiredService<IPurchaseService>();
                        await purchaseService.MarcarComprasVencidasAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ocurrió un error en el servicio de vencimiento de compras.");
                }

                // Para pruebas, se ejecuta cada minuto. Para producción, cambiar a TimeSpan.FromHours(24).
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            _logger.LogInformation("Servicio de Vencimiento de Compras detenido.");
        }
    }
}
