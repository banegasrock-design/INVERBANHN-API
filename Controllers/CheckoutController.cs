using System.Security.Cryptography;
using System.Text;
using Application.DTOs.Sales;
using Application.Interfaces;
using Dapper;
using Infrastructure.Data.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private readonly ISqlStoredProcedureRepository _spRepository;
    private readonly INotificationService _notificationService;
    private readonly DapperContext _dapperContext;
    private readonly string _secretKey;

    public CheckoutController(
        ISqlStoredProcedureRepository spRepository,
        INotificationService notificationService,
        DapperContext dapperContext,
        IConfiguration configuration)
    {
        _spRepository = spRepository;
        _notificationService = notificationService;
        _dapperContext = dapperContext;
        _secretKey = configuration["PixelPaySettings:SecretKey"] ?? string.Empty;
    }

    /// <summary>
    /// Recibe la notificación de pago de PixelPay.
    /// Valida la integridad de los datos mediante Hash MD5.
    /// Actualiza el estado de la venta y notifica al usuario.
    /// </summary>
    [HttpPost("pixelpay-callback")]
    public async Task<IActionResult> PixelPayCallback([FromBody] PixelPayNotification notification)
    {
        // 1. Validar integridad de los datos (Hash MD5)
        // El orden típico de concatenación en PixelPay: SecretKey + OrderId + Amount + Currency
        string rawData = $"{_secretKey}{notification.OrderId}{notification.Amount:F2}{notification.Currency}";
        string calculatedHash = ComputeMd5Hash(rawData);

        if (!string.Equals(calculatedHash, notification.Hash, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { Message = "Hash de seguridad inválido. Los datos podrían haber sido alterados." });
        }

        // 2. Verificar que el resultado sea exitoso
        if (notification.Result != "success" && notification.Result != "approved")
        {
            return Ok(new { Message = "Notificación recibida, pero el pago no fue exitoso." });
        }

        // 3. Ejecutar Procedimiento Almacenado [Sales].[usp_Finalizar_Venta_Exitosa]
        var parameters = new DynamicParameters();
        parameters.Add("@Order_ID", notification.OrderId);
        parameters.Add("@Referencia_Bancaria", notification.BankReference);

        try
        {
            // Set session context explicitly for auditing trigger
            using var connection = _dapperContext.CreateConnection();
            connection.Open();
            
            // Determinamos un UsuarioID (desde Claims o fallback si es un webhook anónimo)
            var usuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "PixelPay_System";
            
            await connection.ExecuteAsync(
                "EXEC sp_set_session_context 'UsuarioID', @UsuarioID",
                new { UsuarioID = usuarioId });

            await connection.ExecuteAsync("[Sales].[usp_Finalizar_Venta_Exitosa]", parameters, commandType: System.Data.CommandType.StoredProcedure);

            // 4. Obtener el Customer_ID para la notificación SignalR
            int customerId = await GetCustomerIdFromOrder(notification.OrderId);

            if (customerId > 0)
            {
                // 5. Disparar notificación SignalR
                await _notificationService.NotifyPaymentConfirmedAsync(customerId, notification.OrderId);
            }

            return Ok(new { Message = "Pago procesado correctamente." });
        }
        catch (Exception ex)
        {
            // Log error (not implemented here)
            return StatusCode(500, new { Message = "Error al procesar la finalización de la venta.", Details = ex.Message });
        }
    }

    private string ComputeMd5Hash(string input)
    {
        using var md5 = MD5.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);

        var sb = new StringBuilder();
        foreach (byte b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    private async Task<int> GetCustomerIdFromOrder(string orderId)
    {
        using var connection = _dapperContext.CreateConnection();
        // Nota: Asumiendo que Order_ID es el identificador único en la tabla Orders
        return await connection.QuerySingleOrDefaultAsync<int>(
            "SELECT Customer_ID FROM [Sales].[Orders] WHERE Order_ID = @OrderId",
            new { OrderId = orderId });
    }
}
