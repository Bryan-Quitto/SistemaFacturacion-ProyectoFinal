using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string toEmail, string userName, string temporaryPassword);
        Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
        Task SendInvoiceEmailAsync(string toEmail, string clienteNombre, string numeroFactura, Guid invoiceId, byte[] pdfBytes, string xmlSignedContent);
        Task SendCreditNoteEmailAsync(string toEmail, string clienteNombre, string numeroNC, Guid ncId, byte[] pdfBytes, string xmlSignedContent);
        Task SendPaymentReminderEmailAsync(string toEmail, string clienteNombre, string numeroFactura, decimal total, decimal saldoPendiente, DateTime fechaVencimiento);
        Task SendCustomerConfirmationEmailAsync(string toEmail, string customerName, string confirmationLink);
        Task SendPaymentConfirmationEmailAsync(string toEmail, string clienteNombre, string numeroFactura, decimal monto, string fecha, string referencia);
        Task SendCustomerTemporaryPasswordEmailAsync(string toEmail, string customerName, string temporaryPassword);
    }
}
