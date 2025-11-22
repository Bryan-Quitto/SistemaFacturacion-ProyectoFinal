using System.Threading.Tasks;

namespace FacturasSRI.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string toEmail, string userName, string temporaryPassword);
        Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
        Task SendInvoiceEmailAsync(string toEmail, string clienteNombre, string numeroFactura, Guid invoiceId, byte[] pdfBytes, string xmlSignedContent);
        Task SendCreditNoteEmailAsync(string toEmail, string clienteNombre, string numeroNC, Guid ncId, byte[] pdfBytes, string xmlSignedContent);

    }
}
