using System.Threading.Tasks;

namespace InverbanHN.Shared.Services
{
    public interface IEmailService
    {
        /// <summary>
        /// Envía un correo electrónico transaccional mediante SendGrid.
        /// </summary>
        /// <param name="toEmail">Correo electrónico del destinatario</param>
        /// <param name="subject">Asunto del correo</param>
        /// <param name="htmlContent">Cuerpo del correo en HTML</param>
        /// <returns>True si el correo se envió con éxito o procesó sin errores fatales; de lo contrario False.</returns>
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent);
    }
}
