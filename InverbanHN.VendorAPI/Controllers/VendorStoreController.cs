using System;
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
    [Route("api/vendor/store")]
    [Authorize(Roles = "StoreAdmin,StoreOperator")]
    public class VendorStoreController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;

        public VendorStoreController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/vendor/store/profile
        /// Consulta el perfil del comercio correspondiente al Store_ID del JWT.
        /// Retorna datos de identidad, contacto e imágenes publicitarias.
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                var sql = @"
                    SELECT 
                        Store_ID AS StoreId,
                        Store_Name AS StoreName,
                        RTN AS Rtn,
                        Inventory_Mode AS InventoryMode,
                        Billing_Type AS BillingType,
                        COALESCE(Logo_Url, Store_Logo) AS LogoUrl,
                        Banner_Url AS BannerUrl,
                        COALESCE(Phone_Number, Telefono, Support_Phone) AS PhoneNumber,
                        COALESCE(Support_Email, Email) AS SupportEmail,
                        Address,
                        Description,
                        ISNULL(Is_Active, 1) AS IsActive
                    FROM [Core].[Stores]
                    WHERE Store_ID = @StoreId";

                var profile = await connection.QuerySingleOrDefaultAsync<StoreProfileResponseDto>(sql, new { StoreId = storeId });

                if (profile == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Tienda no encontrada",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"No se encontró la información del comercio con Store_ID {storeId}."
                    });
                }

                return Ok(profile);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetProfile");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener perfil", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/vendor/store/profile
        /// Actualiza Store_Name, Logo_Url, Banner_Url, Phone_Number, Support_Email, Address, Description.
        /// PROTECCIÓN: No permite cambiar RTN ni Billing_Type desde este endpoint (exclusivos de SuperAdmin).
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateStoreProfileRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría: sp_set_session_context 'UsuarioID'
                await SetAuditContextAsync(connection);

                var sql = @"
                    UPDATE [Core].[Stores]
                    SET Store_Name = @StoreName,
                        Logo_Url = COALESCE(@LogoUrl, Logo_Url, Store_Logo),
                        Store_Logo = COALESCE(@LogoUrl, Store_Logo, Logo_Url),
                        Banner_Url = COALESCE(@BannerUrl, Banner_Url),
                        Phone_Number = COALESCE(@PhoneNumber, Phone_Number, Telefono),
                        Support_Email = COALESCE(@SupportEmail, Support_Email, Email),
                        Address = COALESCE(@Address, Address),
                        Description = COALESCE(@Description, Description)
                    WHERE Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    request.StoreName,
                    request.LogoUrl,
                    request.BannerUrl,
                    request.PhoneNumber,
                    request.SupportEmail,
                    request.Address,
                    request.Description,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Actualización fallida",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"La tienda ID {storeId} no fue encontrada."
                    });
                }

                return await GetProfile();
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "UpdateProfile");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al actualizar perfil de tienda", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/vendor/store/sar-settings
        /// Consulta la configuración fiscal SAR (CAI, rangos autorizados y fecha límite) de la tienda.
        /// </summary>
        [HttpGet("sar-settings")]
        public async Task<IActionResult> GetSarSettings()
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                var sql = @"
                    SELECT 
                        Store_ID AS StoreId,
                        RTN AS Rtn,
                        CAI AS Cai,
                        Range_Start AS RangeStart,
                        Range_End AS RangeEnd,
                        CAI_Expiry_Date AS CaiExpiryDate,
                        Billing_Type AS BillingType
                    FROM [Core].[Stores]
                    WHERE Store_ID = @StoreId";

                var settings = await connection.QuerySingleOrDefaultAsync<StoreSarSettingsDto>(sql, new { StoreId = storeId });

                if (settings == null)
                {
                    return NotFound(new ProblemDetails { Title = "Configuración SAR no encontrada", Detail = $"Tienda ID {storeId} no registrada." });
                }

                return Ok(settings);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetSarSettings");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener datos SAR", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/vendor/store/sar-settings
        /// Actualiza la configuración del CAI, rangos autorizados y fecha límite del SAR.
        /// </summary>
        [HttpPut("sar-settings")]
        public async Task<IActionResult> UpdateSarSettings([FromBody] UpdateStoreSarSettingsRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var sql = @"
                    UPDATE [Core].[Stores]
                    SET RTN = COALESCE(@Rtn, RTN),
                        CAI = COALESCE(@Cai, CAI),
                        Range_Start = COALESCE(@RangeStart, Range_Start),
                        Range_End = COALESCE(@RangeEnd, Range_End),
                        CAI_Expiry_Date = COALESCE(@CaiExpiryDate, CAI_Expiry_Date),
                        Billing_Type = COALESCE(@BillingType, Billing_Type)
                    WHERE Store_ID = @StoreId";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    request.Rtn,
                    request.Cai,
                    request.RangeStart,
                    request.RangeEnd,
                    request.CaiExpiryDate,
                    request.BillingType,
                    StoreId = storeId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails { Title = "Actualización SAR fallida", Detail = "Tienda no encontrada." });
                }

                return await GetSarSettings();
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "UpdateSarSettings");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al actualizar datos SAR", Detail = ex.Message });
            }
        }
    }
}
