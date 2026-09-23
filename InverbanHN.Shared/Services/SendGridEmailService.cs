using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace InverbanHN.Shared.Services
{
    public class SendGridEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SendGridEmailService> _logger;

        public SendGridEmailService(IConfiguration configuration, ILogger<SendGridEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("Intento de envío de correo cancelado: la dirección de correo vacía.");
                return false;
            }

            try
            {
                var apiKey = _configuration["SendGrid:ApiKey"] 
                             ?? _configuration["SendGridApiKey"] 
                             ?? "SG.MockApiKeyForDevelopment_2026_InverbanHN";

                var senderEmail = _configuration["SendGrid:SenderEmail"] 
                                  ?? _configuration["SenderEmail"] 
                                  ?? "no-reply@inverbanhn.com";

                var senderName = _configuration["SendGrid:SenderName"] 
                                 ?? "InverbanHN Marketplace";

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(senderEmail, senderName);
                var to = new EmailAddress(toEmail);

                var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);

                var response = await client.SendEmailAsync(msg);

                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted)
                {
                    _logger.LogInformation("Correo transaccional enviado exitosamente a {ToEmail} con Asunto: '{Subject}'. Status: {StatusCode}", toEmail, subject, response.StatusCode);
                    return true;
                }
                else
                {
                    var body = await response.Body.ReadAsStringAsync();
                    _logger.LogError("Error al enviar correo vía SendGrid a {ToEmail}. Status: {StatusCode}, Body: {ResponseBody}", toEmail, response.StatusCode, body);
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Manejo Defensivo de Errores: Registra la falla pero NO rompe el flujo transaccional principal
                _logger.LogError(ex, "Excepción capturada al enviar correo transaccional SendGrid a {ToEmail}.", toEmail);
                return false;
            }
        }
    }
}
