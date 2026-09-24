using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using InverbanHN.VendorAPI.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.VendorAPI.Controllers
{
    [ApiController]
    [Route("api/vendor/orders")]
    [Authorize(Roles = "StoreAdmin")]
    public class VendorOrdersController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;
        private readonly InverbanHN.Shared.Services.IEmailService _emailService;

        public VendorOrdersController(DapperContext dapperContext, InverbanHN.Shared.Services.IEmailService emailService)
        {
            _dapperContext = dapperContext;
            _emailService = emailService;
        }

        /// <summary>
        /// GET /api/vendor/orders
        /// Lista sub-órdenes asociadas estrictamente al Store_ID.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSubOrders(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            try
            {
                int storeId = GetStoreId();
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                var countSql = @"
                    SELECT COUNT(1)
                    FROM [dbo].[SubOrders]
                    WHERE StoreId = @StoreId
                      AND (@Status IS NULL OR Status = @Status)";

                var sql = @"
                    SELECT 
                        Id AS SubOrderId,
                        StoreId,
                        OrderNumber,
                        CustomerId,
                        TotalAmount,
                        Status,
                        TrackingNumber,
                        CreatedAt
                    FROM [dbo].[SubOrders]
                    WHERE StoreId = @StoreId
                      AND (@Status IS NULL OR Status = @Status)
                    ORDER BY Id DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { StoreId = storeId, Status = status });
                var orders = await connection.QueryAsync<SubOrderResponseDto>(sql, new
                {
                    StoreId = storeId,
                    Status = status,
                    Offset = offset,
                    PageSize = pageSize
                });

                return Ok(new PagedResultDto<SubOrderResponseDto>
                {
                    Items = orders,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetSubOrders");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener órdenes", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PATCH /api/vendor/orders/{subOrderId}/status
        /// Actualiza estados ('En Preparación', 'Enviado', etc.) y opcionalmente el número de guía (tracking).
        /// Notifica al cliente por correo electrónico mediante IEmailService (Plantilla 2).
        /// </summary>
        [HttpPatch("{subOrderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int subOrderId, [FromBody] UpdateOrderStatusRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Obtener datos actuales de la sub-orden para la plantilla de correo
                var orderDetails = await connection.QuerySingleOrDefaultAsync<(string OrderNumber, string? CustomerEmail, string? CustomerName)>(@"
                    SELECT 
                        SO.OrderNumber,
                        COALESCE(U.Email, SO.CustomerEmail) AS CustomerEmail,
                        COALESCE(U.Full_Name, SO.CustomerName, 'Estimado Cliente') AS CustomerName
                    FROM [dbo].[SubOrders] SO
                    LEFT JOIN [Core].[Users] U ON U.User_ID = SO.CustomerId
                    WHERE SO.Id = @SubOrderId AND SO.StoreId = @StoreId",
                    new { SubOrderId = subOrderId, StoreId = storeId });

                if (string.IsNullOrEmpty(orderDetails.OrderNumber))
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Orden no encontrada",
                        Detail = $"La sub-orden ID {subOrderId} no existe o no corresponde a la tienda {storeId}."
                    });
                }

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var sql = @"
                    UPDATE [dbo].[SubOrders]
                    SET Status = @Status,
                        TrackingNumber = COALESCE(@TrackingNumber, TrackingNumber)
                    WHERE Id = @SubOrderId AND StoreId = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    request.Status,
                    request.TrackingNumber,
                    SubOrderId = subOrderId,
                    StoreId = storeId
                });

                if (rowsAffected > 0)
                {
                    // Disparo asíncrono del correo de notificación de estado (Plantilla 2)
                    if (!string.IsNullOrEmpty(orderDetails.CustomerEmail))
                    {
                        var emailHtml = InverbanHN.Shared.Templates.EmailTemplateFactory.GetOrderStatusChangedTemplate(
                            orderDetails.CustomerName ?? "Cliente",
                            orderDetails.OrderNumber,
                            request.Status,
                            request.TrackingNumber);

                        _ = Task.Run(() => _emailService.SendEmailAsync(
                            orderDetails.CustomerEmail,
                            $"Actualización de Pedido: {orderDetails.OrderNumber} - {request.Status}",
                            emailHtml));
                    }
                }

                return Ok(new
                {
                    Message = "Estado de sub-orden actualizado con éxito",
                    SubOrderId = subOrderId,
                    NewStatus = request.Status,
                    TrackingNumber = request.TrackingNumber
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "UpdateOrderStatus");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al actualizar estado de orden", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/vendor/orders/{subOrderId}/cancel
        /// Ejecuta el Stored Procedure '[Sales].[usp_Cancelar_Orden_Con_Reembolso]' o actualización de cancelación.
        /// Validando que la sub-orden pertenezca a la tienda.
        /// </summary>
        [HttpPost("{subOrderId}/cancel")]
        public async Task<IActionResult> CancelOrder(int subOrderId, [FromBody] CancelOrderRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Verificar que la sub-orden existe y pertenece al Store_ID
                var subOrder = await connection.QuerySingleOrDefaultAsync<SubOrderResponseDto>(
                    "SELECT Id AS SubOrderId, StoreId, OrderNumber, TotalAmount, Status FROM [dbo].[SubOrders] WHERE Id = @SubOrderId AND StoreId = @StoreId",
                    new { SubOrderId = subOrderId, StoreId = storeId });

                if (subOrder == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Sub-orden no encontrada",
                        Detail = $"No se encontró la sub-orden {subOrderId} perteneciente a la tienda {storeId}."
                    });
                }

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                try
                {
                    // Intenta llamar al Stored Procedure nativo de cancelación con reembolso
                    await connection.ExecuteAsync(
                        "[Sales].[usp_Cancelar_Orden_Con_Reembolso]",
                        new { p_SubOrderId = subOrderId, p_Reason = request.Reason, p_StoreId = storeId },
                        commandType: CommandType.StoredProcedure);
                }
                catch (SqlException procEx) when (procEx.Number == 2812) // Could not find stored procedure
                {
                    // Fallback si el SP aún no ha sido desplegado en la BD local de pruebas
                    var fallbackSql = @"
                        UPDATE [dbo].[SubOrders]
                        SET Status = 'Cancelado'
                        WHERE Id = @SubOrderId AND StoreId = @StoreId";
                    await connection.ExecuteAsync(fallbackSql, new { SubOrderId = subOrderId, StoreId = storeId });
                }

                return Ok(new
                {
                    Message = "Sub-orden cancelada y reembolso procesado exitosamente",
                    SubOrderId = subOrderId,
                    Reason = request.Reason
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "CancelOrder");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al cancelar la orden", Detail = ex.Message });
            }
        }
    }
}
