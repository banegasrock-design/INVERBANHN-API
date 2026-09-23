using Application.DTOs.Admin;
using Dapper;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/admin/apikeys")]
[Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd", Roles = "SuperAdmin")]
public class ApiKeyController : ControllerBase
{
    private readonly DapperTransactionHelper _dapperTx;
    private readonly IConfiguration _config;

    public ApiKeyController(DapperTransactionHelper dapperTx, IConfiguration config)
    {
        _dapperTx = dapperTx;
        _config = config;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateApiKey([FromBody] ApiKeyGenerateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "UnknownAdmin";

        // Generate cryptographically secure API Key
        var plainTextKey = GenerateSecureKey();
        var hashedKey = HashKey(plainTextKey);

        try
        {
            var newKeyId = await _dapperTx.ExecuteWithAuditAsync(adminId, async (conn, tx) =>
            {
                string sql = @"
                    DECLARE @StoreId INT;
                    SELECT TOP 1 @StoreId = Store_ID FROM [Core].[Stores] WHERE Store_Name = 'INVERBANHN';
                    IF @StoreId IS NULL
                    BEGIN
                        SELECT TOP 1 @StoreId = Store_ID FROM [Core].[Stores] ORDER BY Store_ID;
                    END

                    INSERT INTO [Core].[Api_Keys] (API_Key_Hash, Client_Name, Created_At, Is_Active, Store_ID)
                    OUTPUT INSERTED.Key_ID
                    VALUES (@KeyHash, @ClientName, @CreatedAt, 1, @StoreId);";

                return await conn.QuerySingleAsync<int>(sql, new
                {
                    KeyHash = hashedKey,
                    dto.ClientName,
                    CreatedAt = DateTime.UtcNow
                }, transaction: tx);
            });

            var result = new ApiKeyResultDto
            {
                Id = newKeyId,
                PlainTextKey = plainTextKey,
                ClientName = dto.ClientName,
                Role = dto.Role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Error interno del servidor", Details = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetApiKeys()
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(
            _config.GetConnectionString("DefaultConnection")
        );

        string sql = @"
            SELECT 
                Key_ID, Client_Name, Is_Active, Created_At
            FROM [Core].[Api_Keys]
            ORDER BY Key_ID DESC";

        var keys = await connection.QueryAsync(sql);
        return Ok(keys);
    }

    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> ToggleApiKey(int id)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "UnknownAdmin";

        try
        {
            await _dapperTx.ExecuteWithAuditAsync(adminId, async (conn, tx) =>
            {
                string sql = @"
                    UPDATE [Core].[Api_Keys]
                    SET Is_Active = CASE WHEN Is_Active = 1 THEN 0 ELSE 1 END
                    WHERE Key_ID = @Id;";

                await conn.ExecuteAsync(sql, new { Id = id }, transaction: tx);
            });

            return Ok(new { Message = "Estado de API Key actualizado" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Error interno del servidor", Details = ex.Message });
        }
    }

    private string GenerateSecureKey()
    {
        const string prefix = "inv_live_";
        var keyBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }
        return prefix + Convert.ToBase64String(keyBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private string HashKey(string key)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hashedBytes);
    }
}
