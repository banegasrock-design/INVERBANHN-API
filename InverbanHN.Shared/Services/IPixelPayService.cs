using System.Threading.Tasks;
using InverbanHN.Shared.DTOs;

namespace InverbanHN.Shared.Services
{
    public interface IPixelPayService
    {
        /// <summary>
        /// Genera un enlace de pago (Payment Link) en la API v2 de PixelPay.
        /// </summary>
        /// <param name="orderId">ID numérico o correlativo de la orden</param>
        /// <param name="monto">Monto total de la compra</param>
        /// <param name="clienteEmail">Correo electrónico del comprador</param>
        /// <param name="clienteNombre">Nombre completo del comprador</param>
        /// <param name="orderNumber">Número de orden formato string (ej. ORD-12345678)</param>
        /// <returns>Objeto PixelPayPaymentResponseDto con la URL de pago generada o el resultado</returns>
        Task<PixelPayPaymentResponseDto> GenerarLinkPagoAsync(
            int orderId, 
            decimal monto, 
            string clienteEmail, 
            string clienteNombre, 
            string? orderNumber = null);

        /// <summary>
        /// Valida la firma enviada por PixelPay en solicitudes de Webhook o callbacks.
        /// </summary>
        /// <param name="orderId">ID/Número de la orden</param>
        /// <param name="status">Estado de la transacción</param>
        /// <param name="receivedHash">Firma / Hash recibido en la solicitud</param>
        /// <returns>True si la firma calculada coincide con la recibida; de lo contrario False.</returns>
        bool ValidarFirmaWebhook(string orderId, string status, string receivedHash);
    }
}
