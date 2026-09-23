using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Application.Interfaces;
using Application.DTOs.Wallet;
using Application.Common;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase
{
    private readonly IWalletManagementService _walletService;
    private readonly IMemoryCache _cache;

    // Prefijo para las claves de bloqueo en cache
    private const string LockoutPrefix = "GiftCard_Lockout_";
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public WalletController(IWalletManagementService walletService, IMemoryCache cache)
    {
        _walletService = walletService;
        _cache = cache;
    }

    [HttpPost("{customerId}/redeem")]
    public async Task<IActionResult> RedeemGiftCard(int customerId, [FromBody] RedeemGiftCardRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // 1. Capturar la IP del usuario desde el HttpContext
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        // 2. Verificar si el usuario está bloqueado por intentos fallidos
        var lockoutKey = $"{LockoutPrefix}{customerId}";
        if (_cache.TryGetValue(lockoutKey, out DateTime lockoutExpiry))
        {
            var remaining = lockoutExpiry - DateTime.UtcNow;
            return StatusCode(429, new
            {
                Error = "Demasiados intentos fallidos. Su cuenta ha sido bloqueada temporalmente.",
                MinutosRestantes = Math.Ceiling(remaining.TotalMinutes)
            });
        }

        // 3. Invocar el servicio que llama al Stored Procedure
        var result = await _walletService.RedeemGiftCardAsync(customerId, request.Code, ipAddress);

        if (!result.IsSuccess)
        {
            // 4. Si el SP devolvió "Demasiados intentos fallidos", activar bloqueo
            if (result.Error!.Contains("Demasiados intentos fallidos", StringComparison.OrdinalIgnoreCase))
            {
                var expiry = DateTime.UtcNow.Add(LockoutDuration);
                _cache.Set(lockoutKey, expiry, LockoutDuration);

                return StatusCode(429, new
                {
                    Error = result.Error,
                    MinutosRestantes = LockoutDuration.TotalMinutes
                });
            }

            return BadRequest(new { Error = result.Error });
        }

        return Ok(new { Message = "Gift Card canjeada exitosamente.", NuevoSaldo = result.Data });
    }

    [HttpPost("{customerId}/mixed-payment")]
    public async Task<IActionResult> ProcessMixedPayment(int customerId, [FromBody] MixedPaymentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var response = await _walletService.ProcessMixedPaymentAsync(customerId, request);
            return Ok(response);
        }
        catch (Exception ex) when (ex.Message.Contains("Conflicto de concurrencia"))
        {
            return Conflict(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
