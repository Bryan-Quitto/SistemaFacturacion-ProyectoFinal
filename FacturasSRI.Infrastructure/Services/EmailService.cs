using FacturasSRI.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Text;
using System.Threading.Tasks;

namespace FacturasSRI.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _apiKey;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _apiKey = _configuration["SendGrid:ApiKey"] ?? throw new ArgumentNullException("SendGrid:ApiKey");
            _fromEmail = _configuration["SendGrid:FromEmail"] ?? throw new ArgumentNullException("SendGrid:FromEmail");
            _fromName = _configuration["SendGrid:FromName"] ?? "FacturasSRI";
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlContent, string plainTextContent)
        {
            var client = new SendGridClient(_apiKey);
            var msg = new SendGridMessage()
            {
                From = new EmailAddress(_fromEmail, _fromName),
                Subject = subject,
                PlainTextContent = plainTextContent,
                HtmlContent = htmlContent
            };
            msg.AddTo(new EmailAddress(toEmail));

            await client.SendEmailAsync(msg);
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string userName, string temporaryPassword)
        {
            var subject = $"¡Bienvenido a {_fromName}!";
            var baseUrl = _configuration["App:BaseUrl"] ?? "http://localhost:5000";
            string loginUrl = $"{baseUrl}/login";

            var plainTextContent = $"Hola {userName},\n\nTu cuenta para {_fromName} ha sido creada.\n\nUsuario: {toEmail}\nContraseña Temporal: {temporaryPassword}\n\nInicia sesión aquí: {loginUrl}\n\nPor seguridad, cambia tu contraseña después de iniciar sesión.";

            var htmlContent = BuildEmailTemplate($"¡Bienvenido, {userName}!",
                "<p>Tu cuenta ha sido creada exitosamente. A continuación encontrarás tus credenciales de acceso:</p>" +
                $"<div style='background-color: #f9f9f9; padding: 15px 20px; border-radius: 5px; border: 1px solid #eeeeee; text-align: center;'>" +
                $"<p style='margin: 5px 0;'><strong>Usuario:</strong> {toEmail}</p>" +
                $"<p style='margin: 5px 0;'><strong>Contraseña Temporal:</strong> <span style='font-size: 18px; font-weight: bold; color: #004a99;'>{temporaryPassword}</span></p>" +
                "</div>" +
                "<p>Por tu seguridad, te recomendamos cambiar esta contraseña la primera vez que inicies sesión.</p>",
                loginUrl, "Iniciar Sesión");

            await SendEmailAsync(toEmail, subject, htmlContent, plainTextContent);
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            var subject = $"Restablecimiento de Contraseña para {_fromName}";

            var plainTextContent = $"Hola {userName},\n\nHas solicitado restablecer tu contraseña. Haz clic en el siguiente enlace para continuar:\n{resetLink}\n\nSi no solicitaste esto, puedes ignorar este correo.\n\nEl enlace expirará en 30 minutos.";

            var htmlContent = BuildEmailTemplate("Solicitud de Restablecimiento de Contraseña",
                $"<p>Hola {userName},</p>" +
                "<p>Hemos recibido una solicitud para restablecer la contraseña de tu cuenta. Haz clic en el botón de abajo para elegir una nueva contraseña.</p>" +
                "<p>Si no has sido tú, puedes ignorar este correo de forma segura.</p>" +
                "<p style='font-size: 12px; color: #888888;'>Este enlace es válido por 30 minutos.</p>",
                resetLink, "Restablecer Contraseña");

            await SendEmailAsync(toEmail, subject, htmlContent, plainTextContent);
        }

        public async Task SendInvoiceEmailAsync(string toEmail, string clienteNombre, string numeroFactura, Guid invoiceId, byte[] pdfBytes, string xmlSignedContent)
        {
            if (string.IsNullOrWhiteSpace(toEmail) || toEmail == "consumidorfinal@example.com")
            {
                return;
            }

            var baseUrl = _configuration["App:BaseUrl"] ?? "https://tu-dominio.com";
            baseUrl = baseUrl.TrimEnd('/');
            var downloadLink = $"{baseUrl}/api/public/invoice-ride/{invoiceId}";

            var subject = $"Comprobante Electrónico - Factura {numeroFactura}";
            var plainTextContent = $"Estimado(a) {clienteNombre},\n\nAdjunto encontrará su factura electrónica No. {numeroFactura}.\n\nGracias por su compra.";

            var htmlContent = BuildEmailTemplate("Nuevo Comprobante Electrónico",
            $"<p>Estimado(a) <strong>{clienteNombre}</strong>,</p>" +
            $"<p>Le informamos que se ha generado su comprobante electrónico <strong>No. {numeroFactura}</strong>.</p>" +
            "<p>Adjunto a este correo encontrará los archivos XML y PDF.</p>" +
            "<p>Si lo prefiere, puede descargar la versión visual (RIDE) haciendo clic en el siguiente botón:</p>",
            downloadLink,
            "Descargar Factura PDF");

            var client = new SendGridClient(_apiKey);
            var msg = new SendGridMessage()
            {
                From = new EmailAddress(_fromEmail, _fromName),
                Subject = subject,
                PlainTextContent = plainTextContent,
                HtmlContent = htmlContent
            };
            msg.AddTo(new EmailAddress(toEmail));

            var pdfBase64 = Convert.ToBase64String(pdfBytes);
            msg.AddAttachment($"Factura_{numeroFactura}.pdf", pdfBase64, "application/pdf");

            var xmlBytes = Encoding.UTF8.GetBytes(xmlSignedContent);
            var xmlBase64 = Convert.ToBase64String(xmlBytes);
            msg.AddAttachment($"Factura_{numeroFactura}.xml", xmlBase64, "application/xml");

            await client.SendEmailAsync(msg);
        }

        // === NUEVO MÉTODO IMPLEMENTADO PARA NOTAS DE CRÉDITO ===
        public async Task SendCreditNoteEmailAsync(string toEmail, string clienteNombre, string numeroNC, Guid ncId, byte[] pdfBytes, string xmlSignedContent)
        {
            if (string.IsNullOrWhiteSpace(toEmail) || toEmail == "consumidorfinal@example.com")
            {
                return;
            }

            var baseUrl = _configuration["App:BaseUrl"] ?? "https://tu-dominio.com";
            baseUrl = baseUrl.TrimEnd('/');
            
            // Enlace público para descargar el RIDE de la NC
            var downloadLink = $"{baseUrl}/api/public/nc-ride/{ncId}";

            var subject = $"Comprobante Electrónico - Nota de Crédito {numeroNC}";
            var plainTextContent = $"Estimado(a) {clienteNombre},\n\nAdjunto encontrará su Nota de Crédito Electrónica No. {numeroNC}.\n\nSaludos.";

            var htmlContent = BuildEmailTemplate("Nueva Nota de Crédito",
            $"<p>Estimado(a) <strong>{clienteNombre}</strong>,</p>" +
            $"<p>Le informamos que se ha generado su Nota de Crédito Electrónica <strong>No. {numeroNC}</strong>.</p>" +
            "<p>Adjunto a este correo encontrará los archivos XML y PDF visuales (RIDE).</p>" +
            "<p>Puede descargar el documento directamente desde el siguiente enlace:</p>",
            downloadLink, // URL HABILITADA
            "Descargar Nota Crédito PDF");

            var client = new SendGridClient(_apiKey);
            var msg = new SendGridMessage()
            {
                From = new EmailAddress(_fromEmail, _fromName),
                Subject = subject,
                PlainTextContent = plainTextContent,
                HtmlContent = htmlContent
            };
            msg.AddTo(new EmailAddress(toEmail));

            var pdfBase64 = Convert.ToBase64String(pdfBytes);
            msg.AddAttachment($"NotaCredito_{numeroNC}.pdf", pdfBase64, "application/pdf");

            var xmlBytes = Encoding.UTF8.GetBytes(xmlSignedContent);
            var xmlBase64 = Convert.ToBase64String(xmlBytes);
            msg.AddAttachment($"NotaCredito_{numeroNC}.xml", xmlBase64, "application/xml");

            await client.SendEmailAsync(msg);
        }

        public async Task SendPaymentReminderEmailAsync(string toEmail, string clienteNombre, string numeroFactura, decimal total, decimal saldoPendiente, DateTime fechaVencimiento)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return;
            }

            var subject = $"Recordatorio de Pago - Factura {numeroFactura}";
            var plainTextContent = $"Estimado(a) {clienteNombre},\n\nLe recordamos que su factura No. {numeroFactura} por un total de {total:C} está próxima a vencer.\n\nFecha de Vencimiento: {fechaVencimiento:dd/MM/yyyy}\nSaldo Pendiente: {saldoPendiente:C}\n\nPor favor, realice su pago a la brevedad posible para evitar inconvenientes.\n\nGracias.";

            var htmlContent = BuildEmailTemplate("Recordatorio de Pago",
                $"<p>Estimado(a) <strong>{clienteNombre}</strong>,</p>" +
                $"<p>Este es un recordatorio amistoso sobre su factura pendiente de pago <strong>No. {numeroFactura}</strong>.</p>" +
                "<p>Detalles de la factura:</p>" +
                $"<ul>" +
                $"<li><strong>Monto Total:</strong> {total:C}</li>" +
                $"<li><strong>Saldo Pendiente:</strong> <span style='color: #dc3545; font-weight: bold;'>{saldoPendiente:C}</span></li>" +
                $"<li><strong>Fecha de Vencimiento:</strong> {fechaVencimiento:dd/MM/yyyy}</li>" +
                "</ul>" +
                "<p>Agradeceríamos si pudiera procesar el pago a la brevedad posible. Si ya ha realizado el pago, por favor ignore este mensaje.</p>" +
                "<p>Para realizar su pago o si tiene alguna consulta, no dude en contactarnos.</p>",
                "", // No se necesita un botón de acción principal aquí
                "");

            await SendEmailAsync(toEmail, subject, htmlContent, plainTextContent);
        }

        public async Task SendCustomerConfirmationEmailAsync(string toEmail, string customerName, string confirmationLink)
        {
            var subject = $"Confirma tu cuenta en {_fromName}";

            var plainTextContent = $"Hola {customerName},\n\nGracias por registrarte. Por favor, haz clic en el siguiente enlace para activar tu cuenta:\n{confirmationLink}\n\nSi no te registraste, puedes ignorar este correo.";

            var htmlContent = BuildEmailTemplate("¡Casi listo! Confirma tu correo electrónico",
                $"<p>Hola <strong>{customerName}</strong>,</p>" +
                "<p>Gracias por registrarte en nuestro portal. Solo queda un paso más: por favor, haz clic en el botón de abajo para activar tu cuenta.</p>",
                confirmationLink, "Activar Mi Cuenta");

            await SendEmailAsync(toEmail, subject, htmlContent, plainTextContent);
        }

        private string BuildEmailTemplate(string title, string bodyContent, string buttonUrl, string buttonText)
        {
            string companyName = _configuration["CompanyInfo:DisplayName"] ?? _fromName;
            string currentYear = DateTime.Now.Year.ToString();

            var html = new StringBuilder();
            html.Append("<!DOCTYPE html><html lang='es'><head><meta charset='UTF-8'></head>");
            html.Append($"<body style='font-family: Arial, sans-serif; margin: 0; padding: 0; background-color: #f4f7f6;'>");
            html.Append($"<div style='width: 90%; max-width: 600px; margin: 20px auto; border: 1px solid #dcdcdc; border-radius: 8px; overflow: hidden; background-color: #ffffff;'>");

            html.Append($"<div style='background-color: #004a99; color: #ffffff; padding: 20px 30px; text-align: center;'>");
            html.Append($"<h1 style='margin: 0; font-size: 24px;'>{title}</h1>");
            html.Append("</div>");

            html.Append($"<div style='padding: 30px; color: #333333; line-height: 1.6;'>");
            html.Append(bodyContent);
            
            // Solo mostramos el botón si hay URL
            if (!string.IsNullOrEmpty(buttonUrl) && buttonUrl != "#")
            {
                html.Append($"<p style='text-align: center; margin: 30px 0;'>");
                html.Append($"<a href='{buttonUrl}' target='_blank' style='display: inline-block; background-color: #007bff; color: #ffffff; text-decoration: none; padding: 12px 25px; border-radius: 5px; font-weight: bold;'>{buttonText}</a>");
                html.Append($"</p>");
            }
            
            html.Append($"<p style='margin-top: 30px;'>Saludos,<br>El equipo de {_fromName}</p>");
            html.Append("</div>");

            html.Append($"<div style='background-color: #f4f7f6; color: #888888; padding: 20px 30px; text-align: center; font-size: 12px; border-top: 1px solid #dcdcdc;'>");
            html.Append($"<p style='margin: 5px 0;'>© {currentYear} {companyName}. Todos los derechos reservados.</p>");
            html.Append($"<p style='margin-top: 10px;'>Este es un correo electrónico transaccional. Por favor, no respondas a este mensaje.</p>");
            html.Append("</div>");

            html.Append("</div></body></html>");

            return html.ToString();
        }
    }
}