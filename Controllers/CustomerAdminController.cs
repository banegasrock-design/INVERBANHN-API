using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.DTOs.CustomerAdmin;
using Dapper;
using Infrastructure.Data.Contexts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace INVERBANHN.Controllers;

/// <summary>
/// Controlador para la administración de clientes por parte de administradores.
/// </summary>
[Authorize(AuthenticationSchemes = $"{Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme},ApiKey", Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/admin/customers")]
public class CustomerAdminController : ControllerBase
{
    private readonly DapperContext _dapperContext;
    private readonly MarketplaceDbContext _context;

    public CustomerAdminController(DapperContext dapperContext, MarketplaceDbContext context)
    {
        _dapperContext = dapperContext;
        _context = context;
    }

    /// <summary>
    /// Busca clientes por nombre o email.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchCustomers([FromQuery] string? search)
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            SELECT User_ID AS CustomerId, Full_Name AS FullName, Email, Created_At AS CreatedAt
            FROM [Core].[Users]
            WHERE Role_Name = 'Customer' AND (@Search IS NULL OR Full_Name LIKE '%' + @Search + '%' 
               OR Email LIKE '%' + @Search + '%')
            ORDER BY Full_Name";

        var customers = await connection.QueryAsync<CustomerDto>(sql, new { Search = search });
        return Ok(customers);
    }

    /// <summary>
    /// Obtiene el detalle de un cliente.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerById(int id)
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            SELECT User_ID AS CustomerId, Full_Name AS FullName, Email, Created_At AS CreatedAt
            FROM [Core].[Users]
            WHERE User_ID = @Id AND Role_Name = 'Customer'";

        var customer = await connection.QuerySingleOrDefaultAsync<CustomerDto>(sql, new { Id = id });

        if (customer == null)
            return NotFound(new { Message = "Cliente no encontrado." });

        return Ok(customer);
    }

    /// <summary>
    /// Crea un nuevo cliente global.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCustomer([FromBody] CustomerCreateDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            await SetAuditContext(connection, transaction);

            // Generar hash usando BCrypt
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var sql = @"
                INSERT INTO [Core].[Users] (Full_Name, Email, Password_Hash, Role_Name, Created_At)
                OUTPUT INSERTED.User_ID
                VALUES (@FullName, @Email, @PasswordHash, 'Customer', @CreatedAt)";

            var newId = await connection.QuerySingleAsync<int>(sql, new { 
                FullName = request.FullName, 
                Email = request.Email,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            }, transaction);

            transaction.Commit();
            return CreatedAtAction(nameof(GetCustomerById), new { id = newId }, new { User_ID = newId, Message = "Cliente creado exitosamente." });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            transaction.Rollback();
            return BadRequest(new { Message = "Ya existe un usuario con este correo electrónico." });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(500, new { Message = "Error al crear el cliente.", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Actualiza los datos de un cliente. 
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerRequest request)
    {
        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            await SetAuditContext(connection, transaction);

            var sql = @"
                UPDATE [Core].[Users]
                SET Full_Name = @FullName
                WHERE User_ID = @Id AND Role_Name = 'Customer'";

            var affected = await connection.ExecuteAsync(sql, new { 
                FullName = request.FullName, 
                Id = id 
            }, transaction);

            if (affected == 0)
                return NotFound(new { Message = "Cliente no encontrado." });

            transaction.Commit();
            return NoContent();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(500, new { Message = "Error al actualizar el cliente.", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Elimina un cliente.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            await SetAuditContext(connection, transaction);

            var sql = "DELETE FROM [Core].[Users] WHERE User_ID = @Id AND Role_Name = 'Customer'";
            var affected = await connection.ExecuteAsync(sql, new { Id = id }, transaction);

            if (affected == 0)
                return NotFound(new { Message = "Cliente no encontrado." });

            transaction.Commit();
            return NoContent();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(500, new { Message = "Error al eliminar el cliente.", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene una vista completa de 360 grados del cliente (Perfil, Direcciones, Wallet y Facturas SAR).
    /// </summary>
    [HttpGet("{id}/snapshot")]
    [ProducesResponseType(typeof(CustomerSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerFullSnapshot(int id)
    {
        using var connection = _dapperContext.CreateConnection();

        var sql = @"
            -- 1. Perfil
            SELECT User_ID AS CustomerId, Full_Name AS FullName, Email, Created_At AS CreatedAt
            FROM [Core].[Users] WHERE User_ID = @Id AND Role_Name = 'Customer';

            -- 2. Últimas 5 Direcciones
            SELECT TOP 5 Address_ID AS AddressId, Exact_Address_Line AS AddressLine, City_Name AS City, Is_Default AS IsDefault
            FROM [Core].[Customer_Addresses] WHERE User_ID = @Id;

            -- 3. Saldo Wallet
            SELECT ISNULL(Balance_LPS, 0) FROM [Sales].[Customer_Wallets] WHERE User_ID = @Id;

            -- 4. Últimas 3 Facturas SAR
            SELECT TOP 3 Invoice_Number AS InvoiceNumber, CAI_Snapshot AS CAI, Total_Amount AS TotalAmount, Created_At AS IssuedAt
            FROM [Sales].[Invoices_SAR] WHERE Customer_Name = (SELECT Full_Name FROM [Core].[Users] WHERE User_ID = @Id) ORDER BY Created_At DESC;";

        using var multi = await connection.QueryMultipleAsync(sql, new { Id = id });

        var profile = await multi.ReadSingleOrDefaultAsync<CustomerDto>();
        if (profile == null)
            return NotFound(new { Message = "Cliente no encontrado." });

        var snapshot = new CustomerSnapshotDto
        {
            Profile = profile,
            LastAddresses = (await multi.ReadAsync<CustomerAddressDto>()).ToList(),
            CurrentWalletBalance = await multi.ReadSingleOrDefaultAsync<decimal>(),
            LastInvoices = (await multi.ReadAsync<SarInvoiceDto>()).ToList()
        };

        return Ok(snapshot);
    }

    private async Task SetAuditContext(IDbConnection connection, IDbTransaction transaction)
    {
        // Obtener el ID del usuario o nombre del claim del JWT
        var userName = User.FindFirst(ClaimTypes.Name)?.Value 
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? "Admin_User";

        // sp_set_session_context permite pasar metadatos a los Triggers de SQL Server
        var sql = "EXEC sp_set_session_context @key = N'user_id', @value = @UserId;";
        await connection.ExecuteAsync(sql, new { UserId = userName }, transaction);
    }

    /// <summary>
    /// Restablece la contraseña de un cliente.
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(new { Message = "La contraseña debe tener al menos 6 caracteres." });

        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            await SetAuditContext(connection, transaction);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            var sql = @"
                UPDATE [Core].[Users]
                SET Password_Hash = @PasswordHash
                WHERE User_ID = @Id AND Role_Name = 'Customer'";

            var affected = await connection.ExecuteAsync(sql, new { 
                PasswordHash = passwordHash, 
                Id = id 
            }, transaction);

            if (affected == 0)
                return NotFound(new { Message = "Cliente no encontrado." });

            transaction.Commit();
            return Ok(new { Message = "Contraseña restablecida exitosamente." });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return StatusCode(500, new { Message = "Error al restablecer la contraseña.", Detail = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene el saldo de la billetera y el historial de transacciones de un cliente.
    /// </summary>
    [HttpGet("{id}/wallet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerWallet(int id)
    {
        using var connection = _dapperContext.CreateConnection();
        var customerExists = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT User_ID FROM [Core].[Users] WHERE User_ID = @Id AND Role_Name = 'Customer'", 
            new { Id = id });

        if (customerExists == null)
            return NotFound(new { Message = "Cliente no encontrado." });

        var wallet = await _context.CustomerWallets
            .FirstOrDefaultAsync(w => w.CustomerId == id);

        if (wallet == null)
        {
            wallet = new Domain.Entities.Sales.CustomerWallet
            {
                CustomerId = id,
                Balance = 0
            };
            _context.CustomerWallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        var transactions = await _context.WalletTransactions
            .Where(t => t.WalletId == wallet.Id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(new {
            WalletId = wallet.Id,
            Balance = wallet.Balance,
            Transactions = transactions
        });
    }

    /// <summary>
    /// Realiza un ajuste manual al saldo de la billetera de un cliente.
    /// </summary>
    [HttpPost("{id}/wallet/adjust")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustCustomerWallet(int id, [FromBody] WalletAdjustmentRequest request)
    {
        if (request.Amount == 0)
            return BadRequest(new { Message = "El monto del ajuste debe ser diferente de 0." });

        var wallet = await _context.CustomerWallets
            .FirstOrDefaultAsync(w => w.CustomerId == id);

        if (wallet == null)
        {
            wallet = new Domain.Entities.Sales.CustomerWallet
            {
                CustomerId = id,
                Balance = 0
            };
            _context.CustomerWallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        if (wallet.Balance + request.Amount < 0)
            return BadRequest(new { Message = "Fondos insuficientes para realizar esta operación." });

        wallet.Balance += request.Amount;

        var transaction = new Domain.Entities.Sales.WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = request.Amount,
            TransactionType = "ManualAdjustment",
            ReferenceId = string.IsNullOrWhiteSpace(request.ReferenceId) ? "Ajuste Administrativo" : request.ReferenceId,
            CreatedAt = DateTime.UtcNow
        };

        _context.WalletTransactions.Add(transaction);

        try
        {
            await _context.SaveChangesAsync();
            return Ok(new { 
                Message = "Saldo ajustado exitosamente.", 
                NewBalance = wallet.Balance,
                Transaction = transaction
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { Message = "Conflicto de concurrencia al actualizar el saldo de la billetera. Por favor intente de nuevo." });
        }
    }

    /// <summary>
    /// Obtiene el listado de todas las tarjetas de regalo globales.
    /// </summary>
    [HttpGet("/api/admin/gift-cards")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGiftCards()
    {
        var giftCards = await _context.GiftCards
            .OrderByDescending(g => g.Id)
            .ToListAsync();
        return Ok(giftCards);
    }

    /// <summary>
    /// Crea una nueva tarjeta de regalo global.
    /// </summary>
    [HttpPost("/api/admin/gift-cards")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGiftCard([FromBody] CreateGiftCardRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest(new { Message = "El monto de la tarjeta de regalo debe ser mayor a 0." });

        var code = request.Code?.Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(code))
        {
            var random = new Random();
            code = $"GC-{random.Next(1000, 9999)}-{random.Next(1000, 9999)}";
        }

        var exists = await _context.GiftCards.AnyAsync(g => g.Code == code);
        if (exists)
            return BadRequest(new { Message = $"Ya existe una tarjeta de regalo con el código {code}." });

        var giftCard = new Domain.Entities.Marketing.GiftCard
        {
            Code = code,
            Amount = request.Amount,
            IsRedeemed = false
        };

        _context.GiftCards.Add(giftCard);
        await _context.SaveChangesAsync();

        return Created($"/api/admin/gift-cards/{giftCard.Id}", giftCard);
    }
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class WalletAdjustmentRequest
{
    public decimal Amount { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
}

public class CreateGiftCardRequest
{
    public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
