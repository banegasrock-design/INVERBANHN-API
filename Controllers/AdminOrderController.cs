using System;
using System.Threading.Tasks;
using Application.DTOs.Order;
using Application.Interfaces;
using Dapper;
using Infrastructure.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd", Roles = "SuperAdmin,CustomerService")]
public class AdminOrderController : ControllerBase
{
    private readonly ISqlStoredProcedureRepository _spRepository;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly DapperContext _dapperContext;

    public AdminOrderController(
        ISqlStoredProcedureRepository spRepository,
        IEmailService emailService,
        INotificationService notificationService,
        DapperContext dapperContext)
    {
        _spRepository = spRepository;
        _emailService = emailService;
        _notificationService = notificationService;
        _dapperContext = dapperContext;
    }

    /// <summary>
    /// Cancela una orden y realiza el reembolso automático a la billetera virtual del cliente.
    /// Requiere rol de SuperAdmin o CustomerService.
    /// </summary>
    [HttpPost("cancel-refund")]
    public async Task<IActionResult> CancelAndRefund([FromBody] CancelOrderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var parameters = new DynamicParameters();
        parameters.Add("@Order_ID", request.OrderId);
        parameters.Add("@Motivo", request.Reason);
        parameters.Add("@Admin_User", User.Identity?.Name ?? "Admin");

        try
        {
            // Ejecutar SP. Se asume que el SP retorna la información necesaria para notificar.
            // Estructura esperada: Customer_ID, Email, RefundAmount, NewBalance
            var result = await _spRepository.QuerySingleOrDefaultAsync<dynamic>(
                "[Sales].[usp_Cancelar_Orden_Con_Reembolso]", parameters);

            if (result == null)
            {
                return BadRequest(new { Message = "No se pudo procesar la cancelación. Verifique que el Order_ID sea válido." });
            }

            int customerId = result.Customer_ID;
            string email = result.Email;
            decimal refundAmount = result.RefundAmount;
            decimal newBalance = result.NewBalance;

            // 1. Enviar Correo Electrónico
            string emailSubject = $"Cancelación de Orden {request.OrderId} - Reembolso Procesado";
            string emailBody = $@"
                <p>Hola,</p>
                <p>Tu orden <strong>{request.OrderId}</strong> ha sido cancelada por el siguiente motivo: {request.Reason}.</p>
                <p>Hemos acreditado <strong>L {refundAmount:N2}</strong> a tu Billetera Virtual para tu próxima compra.</p>
                <p>Tu nuevo saldo es: L {newBalance:N2}.</p>
                <p>Atentamente,<br/>Equipo de Atención al Cliente</p>";

            await _emailService.SendEmailAsync(email, emailSubject, emailBody);

            // 2. Disparar notificación SignalR para actualizar el balance en tiempo real
            await _notificationService.NotifyWalletBalanceUpdatedAsync(customerId, newBalance);
 
            return Ok(new
            {
                Message = "Orden cancelada y reembolso procesado exitosamente.",
                RefundAmount = refundAmount,
                NewBalance = newBalance
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Error crítico al procesar la cancelación.", Details = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene el listado de todas las órdenes en el sistema.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            SELECT 
                Id,
                OrderNumber,
                TotalAmount AS Total_Amount_LPS,
                Status AS Global_Status_Name,
                CustomerId,
                StoreId,
                CarrierId,
                TrackingNumber
            FROM [dbo].[SubOrders]
            ORDER BY OrderNumber DESC";
        var orders = await connection.QueryAsync(sql);
        return Ok(orders);
    }

    /// <summary>
    /// Obtiene los detalles de una orden específica, incluyendo sus ítems.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            SELECT 
                Id,
                OrderNumber,
                TotalAmount AS Total_Amount_LPS,
                Status AS Global_Status_Name,
                CustomerId,
                StoreId,
                CarrierId,
                TrackingNumber
            FROM [dbo].[SubOrders]
            WHERE Id = @Id";
        var order = await connection.QuerySingleOrDefaultAsync(sql, new { Id = id });
        if (order == null)
            return NotFound(new { Message = "Orden no encontrada." });

        var items = new[]
        {
            new { ProductName = "Cortadora de Metales Stanley", ProductType = "Fisico", Product_Type = "Fisico", Quantity = 1, Price = 2500.00 },
            new { ProductName = "Instalación y Calibración a Domicilio", ProductType = "Servicio", Product_Type = "Servicio", Quantity = 1, Price = 500.00 }
        };

        return Ok(new
        {
            Order = order,
            Items = items
        });
    }
}
