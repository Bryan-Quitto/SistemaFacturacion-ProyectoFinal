using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

namespace FacturasSRI.Infrastructure.Services
{
    public class StripePaymentService : IStripePaymentService
    {
        private readonly string _apiKey;
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;

        public StripePaymentService(IConfiguration configuration, IDbContextFactory<FacturasSRIDbContext> contextFactory)
        {
            var key = configuration["Stripe:SecretKey"];
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            
            _apiKey = key;
            StripeConfiguration.ApiKey = _apiKey;
            _contextFactory = contextFactory;
        }

        public async Task<string> CreateCheckoutSessionAsync(InvoiceDto facturaDto, string successUrl, string cancelUrl, Guid clienteUserId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var facturaDb = await context.Facturas.FindAsync(facturaDto.Id);
            if (facturaDb == null) throw new Exception("Factura no encontrada");

            var sessionService = new SessionService();
            Session? sessionExistente = null;

            // Calculamos cuánto DEBERÍA cobrar la sesión hoy
            long montoActualRequeridoEnCentavos = (long)(facturaDto.SaldoPendiente * 100);

            // 1. Verificar si ya hay sesión
            if (!string.IsNullOrEmpty(facturaDb.StripeSessionId))
            {
                try 
                {
                    sessionExistente = await sessionService.GetAsync(facturaDb.StripeSessionId);
                }
                catch (StripeException) 
                {
                    sessionExistente = null; 
                }
            }

            // 2. Evaluamos la sesión existente
            if (sessionExistente != null)
            {
                // Si ya está pagada, error
                if (sessionExistente.PaymentStatus == "paid")
                {
                    throw new Exception("Esta factura ya tiene un pago procesado en Stripe.");
                }

                // VALIDACIÓN CRÍTICA (NUEVA): ¿El monto del link viejo coincide con la deuda actual?
                if (sessionExistente.AmountTotal != montoActualRequeridoEnCentavos)
                {
                    // ¡EL MONTO CAMBIÓ! (Alguien pagó $1.00 por otro lado)
                    // Expiramos la sesión vieja para que nadie la pague por error
                    try 
                    {
                        if(sessionExistente.Status == "open") 
                        {
                            await sessionService.ExpireAsync(facturaDb.StripeSessionId);
                        }
                    }
                    catch { /* Ignoramos si ya estaba expirada */ }

                    sessionExistente = null; // Forzamos a crear una nueva abajo
                }
                else if (sessionExistente.Status == "open")
                {
                    // Si el monto es correcto y está abierta, devolvemos el mismo link (Persistencia)
                    return sessionExistente.Url;
                }
            }

            // 3. Crear NUEVA sesión (si no existe, expiró o el monto cambió)
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions 
                        {
                            UnitAmount = montoActualRequeridoEnCentavos, // Usamos el monto calculado
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions 
                            {
                                Name = $"Pago Factura #{facturaDto.NumeroFactura}",
                                Description = $"Pago de saldo pendiente"
                            },
                        },
                        Quantity = 1,
                    },
                },
                Mode = "payment",
                SuccessUrl = successUrl + "&session_id={CHECKOUT_SESSION_ID}", 
                CancelUrl = cancelUrl,
                ClientReferenceId = facturaDto.Id.ToString(), 
                Metadata = new Dictionary<string, string>
                {
                    { "FacturaId", facturaDto.Id.ToString() },
                    { "ClienteUserId", clienteUserId.ToString() },
                    { "NumeroFactura", facturaDto.NumeroFactura }
                }
            };

            Session session = await sessionService.CreateAsync(options);

            facturaDb.StripeSessionId = session.Id;
            await context.SaveChangesAsync();

            return session.Url;
        }

        public async Task<Session> GetSessionAsync(string sessionId)
        {
            var service = new SessionService();
            return await service.GetAsync(sessionId);
        }
    }
}