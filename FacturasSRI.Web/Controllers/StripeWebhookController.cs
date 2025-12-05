using FacturasSRI.Application.Dtos;
using FacturasSRI.Application.Interfaces;
using FacturasSRI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace FacturasSRI.Web.Controllers
{
    [Route("api/webhook")]
    [ApiController]
    public class StripeWebhookController : ControllerBase
    {
        private readonly string _webhookSecret;
        private readonly ICobroService _cobroService;
        private readonly IDbContextFactory<FacturasSRIDbContext> _contextFactory;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(
            IConfiguration config, 
            ICobroService cobroService, 
            IDbContextFactory<FacturasSRIDbContext> contextFactory,
            ILogger<StripeWebhookController> logger)
        {
            var secret = config["Stripe:WebhookSecret"];
            if (string.IsNullOrEmpty(secret)) throw new ArgumentNullException(nameof(secret));
            _webhookSecret = secret;
            
            _cobroService = cobroService;
            _contextFactory = contextFactory;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Index()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], _webhookSecret);

                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session != null) await HandleCheckoutSessionCompleted(session);
                }

                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error procesando el Webhook");
                // Importante: No devolvemos 500 si es error de lógica de negocio para que Stripe no reintente infinitamente,
                // pero si es código, mejor ver el log.
                return StatusCode(500);
            }
        }

        private async Task HandleCheckoutSessionCompleted(Session session)
        {
            if (session.Metadata.TryGetValue("FacturaId", out string? facturaIdStr) &&
                Guid.TryParse(facturaIdStr, out Guid facturaId))
            {
                // 1. BUSCAR EL ID DEL VENDEDOR (USUARIO) QUE CREÓ LA FACTURA
                Guid usuarioVendedorId;
                using (var context = await _contextFactory.CreateDbContextAsync())
                {
                    // Buscamos solo el ID del creador para ser eficientes
                    var facturaInfo = await context.Facturas
                        .Where(f => f.Id == facturaId)
                        .Select(f => new { f.UsuarioIdCreador })
                        .FirstOrDefaultAsync();

                    if (facturaInfo == null)
                    {
                        _logger.LogError($"Factura {facturaId} no encontrada al procesar pago.");
                        return;
                    }
                    usuarioVendedorId = facturaInfo.UsuarioIdCreador;
                }

                // 2. Registrar el cobro usando el ID del Vendedor (Usuario), NO del Cliente
                var nuevoCobro = new RegistrarCobroDto
                {
                    FacturaId = facturaId,
                    Monto = (decimal)(session.AmountTotal ?? 0) / 100m,
                    MetodoDePago = "Tarjeta de Crédito/Débito",
                    Referencia = $"Stripe Webhook: {session.PaymentIntentId}",
                    UsuarioIdCreador = usuarioVendedorId,
                    FechaCobro = DateTime.UtcNow
                };

                try 
                {
                    await _cobroService.RegistrarCobroAsync(nuevoCobro, null, null);
                    _logger.LogInformation($"Pago registrado vía Webhook para Factura {facturaId}");
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("totalmente pagada"))
                    {
                        _logger.LogError(ex, "Error al registrar cobro en DB");
                        throw;
                    }
                }
            }
        }
    }
}