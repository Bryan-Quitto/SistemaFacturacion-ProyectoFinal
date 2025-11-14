using FacturasSRI.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
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

            // En un escenario real, aquí se añadiría logging para la respuesta.
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

        private string BuildEmailTemplate(string title, string bodyContent, string buttonUrl, string buttonText)
        {
            string companyName = _configuration["CompanyInfo:NombreComercial"] ?? _fromName;
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
            html.Append($"<p style='text-align: center; margin: 30px 0;'>");
            html.Append($"<a href='{buttonUrl}' target='_blank' style='display: inline-block; background-color: #007bff; color: #ffffff; text-decoration: none; padding: 12px 25px; border-radius: 5px; font-weight: bold;'>{buttonText}</a>");
            html.Append($"</p>");
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