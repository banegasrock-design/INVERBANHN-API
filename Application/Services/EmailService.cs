using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body)
    {
        // En una implementación real, aquí se usaría SmtpClient, SendGrid, Amazon SES, etc.
        _logger.LogInformation("Enviando correo a {To}. Asunto: {Subject}. Cuerpo: {Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
