using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Infrastructure.Persistence; // Acceso a DB
using Microsoft.EntityFrameworkCore; // Entity Framework
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

namespace FacturasSRI.Infrastructure.Services
{
    public class StripePaymentService : IStripePaymentService
    {
        private readonly string _apiKey;
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory; // <--- Nuevo

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
            
            // 1. Recuperamos la entidad Factura real de la BD para ver si ya tiene sesión
            var facturaDb = await context.Facturas.FindAsync(facturaDto.Id);
            if (facturaDb == null) throw new Exception("Factura no encontrada");

            var sessionService = new SessionService();
            Session? sessionExistente = null;

            // 2. NIVEL NASA: Verificamos si ya hay una sesión guardada
            if (!string.IsNullOrEmpty(facturaDb.StripeSessionId))
            {
                try 
                {
                    sessionExistente = await sessionService.GetAsync(facturaDb.StripeSessionId);
                }
                catch (StripeException) 
                {
                    // Si da error (ej. borraron datos en Stripe test), asumimos que no existe
                    sessionExistente = null; 
                }
            }

            // 3. Evaluamos la sesión existente
            if (sessionExistente != null)
            {
                // Si está ABIERTA (Open), devolvemos EL MISMO LINK. ¡No creamos uno nuevo!
                if (sessionExistente.Status == "open")
                {
                    return sessionExistente.Url;
                }
                // Si ya está PAGADA (Complete), lanzamos error (o devolvemos successUrl)
                if (sessionExistente.PaymentStatus == "paid")
                {
                    throw new Exception("Esta factura ya tiene un pago procesado en Stripe.");
                }
                // Si expiró, dejamos que el código siga y cree una nueva
            }

            // 4. Si no existe o expiró, CREAMOS UNA NUEVA (Tu código original)
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions 
                        {
                            UnitAmount = (long)(facturaDto.SaldoPendiente * 100), 
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

            // 5. GUARDAMOS EL ID DE LA NUEVA SESIÓN EN LA DB
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