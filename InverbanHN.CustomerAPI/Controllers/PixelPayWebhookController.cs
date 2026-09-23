using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using InverbanHN.Shared.DTOs;
using InverbanHN.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InverbanHN.CustomerAPI.Controllers
{
    [ApiController]
    [Route("api/webhooks/pixelpay")]
    [AllowAnonymous]
    public class PixelPayWebhookController : ControllerBase
    {
        private readonly DapperContext _dapperContext;
        private readonly IPixelPayService _pixelPayService;
        private readonly ILogger<PixelPayWebhookController> _logger;

        public PixelPayWebhookController(
            DapperContext dapperContext,
            IPixelPayService pixelPayService,
            ILogger<PixelPayWebhookController> logger)
        {
            _dapperContext = dapperContext;
            _pixelPayService = pixelPayService;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/webhooks/pixelpay
        /// Webhook asíncrono oficial invocado por la pasarela de pagos PixelPay.
        /// Retorna siempre HTTP 200 OK para evitar reintentos duplicados de la pasarela.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ReceivePixelPayWebhook([FromBody] PixelPayWebhookPayloadDto payload)
        {
            _logger.LogInformation("Webhook de PixelPay recibido: OrderId='{OrderId}', TransactionId='{TransactionId}', Status='{Status}'",
                payload?.OrderId, payload?.TransactionId, payload?.Status);

            try
            {
                if (payload == null || string.IsNullOrWhiteSpace(payload.OrderId))
                {
                    _logger.LogWarning("Webhook de PixelPay recibido con payload o OrderId nulo/inválido.");
                    return Ok(new { message = "Payload inválido o vacío pero recibido." });
                }

                // Extraer encabezados de firma opcionales o Hash del body
                string receivedSignature = payload.Signature ?? payload.Hash ?? Request.Headers["x-pixelpay-signature"].ToString();
                string statusClean = payload.Status?.Trim() ?? "Unknown";

                // Validar Firma del Webhook
                bool isSignatureValid = _pixelPayService.ValidarFirmaWebhook(payload.OrderId, statusClean, receivedSignature);
                if (!isSignatureValid)
                {
                    _logger.LogError("AUDITORÍA DE SEGURIDAD: Firma de Webhook PixelPay inválida para Orden '{OrderId}'. Proceso detenido sin modificar base de datos.", payload.OrderId);
                    // Devolver 200 OK para evitar que la pasarela reintente en bucle peticiones maliciosas/inválidas
                    return Ok(new { message = "Firma inválida procesada." });
                }

                using var connection = (SqlConnection)_dapperContext.CreateConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                using var transaction = connection.BeginTransaction();

                try
                {
                    // a. Inyectar contexto de auditoría ejecutando sp_set_session_context 'UsuarioID', 0
                    var setAuditSql = "EXEC sp_set_session_context @key = N'UsuarioID', @value = 0;";
                    await connection.ExecuteAsync(setAuditSql, transaction: transaction);

                    bool isApproved = string.Equals(statusClean, "Completed", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(statusClean, "Approved", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(statusClean, "Paid", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(statusClean, "Success", StringComparison.OrdinalIgnoreCase);

                    string orderStatus = isApproved ? "Pagado" : "Rechazado";
                    string transactionRef = payload.TransactionId ?? $"PX-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

                    // b. Actualizar estado de la orden en [dbo].[SubOrders] o tabla de ventas principal
                    var updateOrderSql = @"
                        UPDATE [dbo].[SubOrders]
                        SET Status = @Status,
                            TrackingNumber = @TransactionRef,
                            UpdatedAt = GETUTCDATE()
                        WHERE OrderNumber = @OrderRef OR CAST(Id AS VARCHAR(50)) = @OrderRef;";

                    int rowsAffected = await connection.ExecuteAsync(updateOrderSql, new
                    {
                        Status = orderStatus,
                        TransactionRef = transactionRef,
                        OrderRef = payload.OrderId
                    }, transaction: transaction);

                    transaction.Commit();

                    _logger.LogInformation("Orden '{OrderId}' actualizada a estado '{Status}' exitosamente por Webhook PixelPay ({RowsAffected} filas modificadas).",
                        payload.OrderId, orderStatus, rowsAffected);
                }
                catch (Exception dbEx)
                {
                    transaction.Rollback();
                    _logger.LogError(dbEx, "Error de Base de Datos al procesar Webhook PixelPay para la Orden '{OrderId}'", payload.OrderId);
                }

                return Ok(new
                {
                    message = "Webhook recibido y procesado correctamente.",
                    orderId = payload.OrderId,
                    status = statusClean
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al procesar Webhook de PixelPay.");
                // Retornar 200 OK inmediatamente para cumplir con la regla arquitectónica de evitar reintentos infinitos
                return Ok(new { message = "Excepción capturada pero Webhook confirmado." });
            }
        }
    }
}
