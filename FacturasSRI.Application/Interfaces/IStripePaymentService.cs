using FacturasSRI.Application.Dtos;

namespace FacturasSRI.Application.Interfaces
{
    public interface IStripePaymentService
    {
        Task<string> CreateCheckoutSessionAsync(InvoiceDto factura, string successUrl, string cancelUrl, Guid clienteUserId);
        Task<Stripe.Checkout.Session> GetSessionAsync(string sessionId);
    }
}