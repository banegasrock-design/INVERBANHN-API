using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.AdminAPI.DTOs;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/stores")]
    [Authorize(Roles = "SuperAdmin")]
    public class AdminStoresController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;

        public AdminStoresController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/admin/stores
        /// Lista paginada de todas las tiendas en el Marketplace.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStores(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            try
            {
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                var countSql = @"
                    SELECT COUNT(1) 
                    FROM [Core].[Stores]
                    WHERE (@Search IS NULL OR Store_Name LIKE '%' + @Search + '%' OR RTN LIKE '%' + @Search + '%')";

                var storesSql = @"
                    SELECT 
                        Store_ID AS StoreId,
                        Store_Name AS StoreName,
                        RTN AS Rtn,
                        ISNULL(Inventory_Mode, 'Tienda') AS InventoryMode,
                        ISNULL(Billing_Type, 'Managed') AS BillingType,
                        CAI,
                        ISNULL(Is_Active, 1) AS IsActive,
                        ISNULL(Created_At, GETUTCDATE()) AS CreatedAt
                    FROM [Core].[Stores]
                    WHERE (@Search IS NULL OR Store_Name LIKE '%' + @Search + '%' OR RTN LIKE '%' + @Search + '%')
                    ORDER BY Store_ID DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { Search = search });
                var stores = await connection.QueryAsync<AdminStoreListItemDto>(storesSql, new
                {
                    Search = search,
                    Offset = offset,
                    PageSize = pageSize
                });

                return Ok(new PagedResultDto<AdminStoreListItemDto>
                {
                    Items = stores,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetStores");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al listar tiendas", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/stores
        /// Registra una nueva tienda (Nombre, RTN, Modo_Inventario).
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateStore([FromBody] CreateAdminStoreRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var insertSql = @"
                    INSERT INTO [Core].[Stores]
                        (Store_Name, RTN, Inventory_Mode, Billing_Type, Is_Active, Created_At)
                    VALUES
                        (@StoreName, @Rtn, @InventoryMode, @BillingType, 1, GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newStoreId = await connection.ExecuteScalarAsync<int>(insertSql, new
                {
                    StoreName = request.StoreName.Trim(),
                    Rtn = request.Rtn?.Trim(),
                    InventoryMode = request.InventoryMode,
                    BillingType = request.BillingType
                });

                await WriteAuditLogAsync(connection, "Core.Stores", "INSERT", $"Tienda '{request.StoreName}' registrada con ID {newStoreId}.");

                return Created($"/api/admin/stores/{newStoreId}", new
                {
                    Message = "Tienda registrada con éxito.",
                    StoreId = newStoreId,
                    StoreName = request.StoreName.Trim(),
                    InventoryMode = request.InventoryMode,
                    BillingType = request.BillingType
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "CreateStore");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al registrar tienda", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/admin/stores/{storeId}/status
        /// Activa o Suspende una tienda (Is_Active = 1 o 0).
        /// Si se suspende, sus productos dejan de mostrarse en la CustomerAPI.
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPut("{storeId}/status")]
        public async Task<IActionResult> UpdateStoreStatus(int storeId, [FromBody] UpdateAdminStoreStatusRequestDto request)
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var updateSql = @"
                    UPDATE [Core].[Stores]
                    SET Is_Active = @IsActive
                    WHERE Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(updateSql, new
                {
                    IsActive = request.IsActive,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Tienda no encontrada",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"No se encontró la tienda con ID {storeId}."
                    });
                }

                string statusText = request.IsActive ? "ACTIVADA" : "SUSPENDIDA";
                await WriteAuditLogAsync(connection, "Core.Stores", "UPDATE_STATUS", $"Estado de tienda {storeId} cambiado a {statusText}.");

                return Ok(new
                {
                    Message = $"Tienda ID {storeId} ha sido {statusText} exitosamente.",
                    StoreId = storeId,
                    IsActive = request.IsActive
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "UpdateStoreStatus");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al actualizar estado de la tienda", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/stores/{storeId}/sar
        /// Configura los rangos iniciales del SAR, CAI y fecha de expiración para la tienda.
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost("{storeId}/sar")]
        public async Task<IActionResult> ConfigureStoreSar(int storeId, [FromBody] ConfigureAdminStoreSarRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var updateSql = @"
                    UPDATE [Core].[Stores]
                    SET CAI = @Cai,
                        Range_Start = @RangeStart,
                        Range_End = @RangeEnd,
                        CAI_Expiry_Date = @CaiExpiryDate
                    WHERE Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(updateSql, new
                {
                    Cai = request.Cai.Trim(),
                    request.RangeStart,
                    request.RangeEnd,
                    request.CaiExpiryDate,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Tienda no encontrada",
                        Detail = $"La tienda ID {storeId} no fue encontrada."
                    });
                }

                await WriteAuditLogAsync(connection, "Core.Stores", "UPDATE_SAR", $"Configuración SAR actualizada para tienda {storeId}. CAI: {request.Cai}.");

                return Ok(new
                {
                    Message = "Parámetros SAR y CAI configurados exitosamente para la tienda.",
                    StoreId = storeId,
                    Cai = request.Cai.Trim(),
                    RangeStart = request.RangeStart,
                    RangeEnd = request.RangeEnd,
                    CaiExpiryDate = request.CaiExpiryDate
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "ConfigureStoreSar");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al configurar SAR", Detail = ex.Message });
            }
        }
    }
}
