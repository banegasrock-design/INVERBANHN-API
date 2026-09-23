using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using Application.DTOs.Store;
using Application.Common;
using Infrastructure.Data.Contexts;

using Microsoft.AspNetCore.Authorization;

namespace INVERBANHN.Controllers;

/// <summary>
/// Administración de Tiendas del Marketplace.
/// </summary>
[ApiController]
[Route("api/admin/stores")]
[Authorize(AuthenticationSchemes = "Bearer,ApiKey,AzureAd", Roles = "SuperAdmin")]
public class AdminStoreController : ControllerBase
{
    private readonly DapperContext _dapperContext;
    private readonly Infrastructure.Data.DapperTransactionHelper _dapperTx;

    public AdminStoreController(DapperContext dapperContext, Infrastructure.Data.DapperTransactionHelper dapperTx)
    {
        _dapperContext = dapperContext;
        _dapperTx = dapperTx;
    }

    /// <summary>
    /// Obtiene la lista de todas las tiendas registradas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(System.Collections.Generic.IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStores()
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            SELECT 
                Store_ID AS Id, 
                Store_Name AS Name, 
                Tax_ID_RTN AS RTN, 
                Inventory_Mode AS InventoryMode, 
                Billing_Type AS BillingType, 
                HasIsrWithholding
            FROM [Core].[Stores]
            ORDER BY Store_ID DESC";
        
        var stores = await connection.QueryAsync(sql);
        return Ok(stores);
    }

    /// <summary>
    /// Crea una nueva tienda con datos legales, configuración operativa y configuración SAR (si aplica).
    /// FluentValidation valida automáticamente el formato del RTN (14 dígitos) y CAI antes de ejecutar.
    /// </summary>
    /// <param name="request">Datos de la tienda a crear.</param>
    /// <returns>ID de la tienda creada.</returns>
    /// <response code="201">Tienda creada exitosamente.</response>
    /// <response code="400">Error de validación (RTN, CAI o datos faltantes).</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStore([FromBody] CreateStoreRequest request)
    {
        // FluentValidation ya validó el request automáticamente antes de llegar aquí.
        // Si el RTN, CAI o rangos son inválidos, el pipeline devolvió 400 antes de este punto.

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "UnknownAdmin";

        // Generar slug amigable para URL (ej. "Mi Tienda" -> "mi-tienda")
        var storeSlug = System.Text.RegularExpressions.Regex.Replace(
            request.StoreName.ToLowerInvariant().Trim(),
            @"[^a-z0-9\s-]", ""
        ).Replace(" ", "-");

        try
        {
            var result = await _dapperTx.ExecuteWithAuditAsync(adminId, async (connection, transaction) =>
            {
                // 1. Insertar la Tienda en [Core].[Stores]
                var insertStoreSql = @"
                    DECLARE @OwnerId INT = @Owner_User_ID;
                    IF @OwnerId IS NULL
                    BEGIN
                        SELECT TOP 1 @OwnerId = User_ID FROM [Core].[Users] WHERE Role_Name = 'SuperAdmin' ORDER BY User_ID;
                    END

                    INSERT INTO [Core].[Stores] 
                        (Store_Name, Store_Slug, Tax_ID_RTN, Inventory_Mode, Billing_Type, Store_Type, Created_At, Owner_User_ID)
                    VALUES 
                        (@Store_Name, @Store_Slug, @Tax_ID_RTN, @Inventory_Mode, @Billing_Type, 'Marketplace', @Created_At, @OwnerId);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var storeId = await connection.QuerySingleAsync<int>(insertStoreSql, new
                {
                    Store_Name = request.StoreName,
                    Store_Slug = storeSlug,
                    Tax_ID_RTN = request.RTN,
                    Inventory_Mode = request.InventoryMode,
                    Billing_Type = request.BillingType,
                    Created_At = DateTime.UtcNow,
                    Owner_User_ID = request.OwnerUserId
                }, transaction);

                // 2. Si es Autoimpresor, insertar la configuración SAR
                if (request.BillingType == "Autoimpresor")
                {
                    var insertSarSql = @"
                        INSERT INTO [Logistics].[Store_SAR_Settings] 
                            (Store_ID, CAI, Range_Start, Range_End, Current_Number, Expiry_Date, Is_Active)
                        VALUES 
                            (@Store_ID, @CAI, @Range_Start, @Range_End, @Current_Number, @Expiry_Date, 1);";

                    await connection.ExecuteAsync(insertSarSql, new
                    {
                        Store_ID = storeId,
                        CAI = request.CAI,
                        Range_Start = request.RangeStart!.Value,
                        Range_End = request.RangeEnd!.Value,
                        Current_Number = request.RangeStart.Value, // El número actual inicia en el inicio del rango
                        Expiry_Date = request.CaiExpiryDate!.Value
                    }, transaction);
                }

                return storeId;
            });

            return CreatedAtAction(nameof(GetStoreById), new { storeId = result }, new
            {
                Message = "Tienda creada exitosamente.",
                StoreId = result,
                StoreName = request.StoreName,
                InventoryMode = request.InventoryMode,
                BillingType = request.BillingType,
                SarConfigured = request.BillingType == "Autoimpresor"
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new { Error = $"Error de base de datos: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = $"Error interno: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene los datos de una tienda por su ID, incluyendo configuración SAR si es Autoimpresor.
    /// </summary>
    /// <param name="storeId">ID de la tienda.</param>
    [HttpGet("{storeId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStoreById(int storeId)
    {
        using var connection = _dapperContext.CreateConnection();

        var storeSql = @"
            SELECT Store_ID, Store_Name, Tax_ID_RTN, Inventory_Mode, Billing_Type, Store_Type, Created_At 
            FROM [Core].[Stores] 
            WHERE Store_ID = @StoreId";

        var store = await connection.QuerySingleOrDefaultAsync(storeSql, new { StoreId = storeId });

        if (store == null)
            return NotFound(new { Error = "La tienda no existe." });

        // Si es Autoimpresor, traer la configuración SAR
        object? sarConfig = null;
        if (store.Billing_Type == "Autoimpresor")
        {
            var sarSql = @"
                SELECT CAI, Range_Start, Range_End, Current_Number, Expiry_Date, Is_Active 
                FROM [Logistics].[Store_SAR_Settings] 
                WHERE Store_ID = @StoreId AND Is_Active = 1";

            sarConfig = await connection.QuerySingleOrDefaultAsync(sarSql, new { StoreId = storeId });
        }

        return Ok(new
        {
            Store = store,
            SarConfiguration = sarConfig
        });
    }
}
