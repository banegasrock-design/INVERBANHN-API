using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.CustomerAPI.DTOs;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.CustomerAPI.Controllers
{
    [ApiController]
    [Route("api/customer/profile")]
    [Authorize(Roles = "Customer,SuperAdmin")]
    public class CustomerProfileController : CustomerBaseController
    {
        private readonly DapperContext _dapperContext;

        public CustomerProfileController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/customer/profile
        /// Obtiene los datos del perfil y libreta de direcciones del comprador autenticado a partir del JWT.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                int userId = GetUserId();
                using var connection = _dapperContext.CreateConnection();

                var userSql = @"
                    SELECT 
                        COALESCE(User_ID, Id) AS UserId,
                        COALESCE(Full_Name, Nombre, 'Cliente InverbanHN') AS FullName,
                        Email,
                        COALESCE(Phone, Telefono, Teléfono) AS Phone,
                        ISNULL(Created_At, GETUTCDATE()) AS MemberSince
                    FROM [Core].[Users]
                    WHERE User_ID = @UserId OR Id = @UserId";

                var profile = await connection.QuerySingleOrDefaultAsync<CustomerProfileResponseDto>(userSql, new { UserId = userId });

                if (profile == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Perfil no encontrado",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"No se encontró la cuenta de usuario para el ID {userId}."
                    });
                }

                var addressSql = @"
                    SELECT 
                        Address_ID AS AddressId,
                        User_ID AS UserId,
                        Address_Line1 AS AddressLine1,
                        Address_Line2 AS AddressLine2,
                        City,
                        Department,
                        Zip_Code AS ZipCode,
                        Is_Default AS IsDefault
                    FROM [Core].[Addresses]
                    WHERE User_ID = @UserId
                    ORDER BY Is_Default DESC, Address_ID DESC";

                var addresses = await connection.QueryAsync<CustomerAddressDto>(addressSql, new { UserId = userId });
                profile.Addresses = addresses.AsList();

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
        /// POST /api/customer/profile/addresses
        /// Crea una nueva dirección de envío para el comprador.
        /// Inyecta auditoría sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost("addresses")]
        public async Task<IActionResult> CreateAddress([FromBody] CreateUpdateAddressRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int userId = GetUserId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                if (request.IsDefault)
                {
                    await connection.ExecuteAsync("UPDATE [Core].[Addresses] SET Is_Default = 0 WHERE User_ID = @UserId", new { UserId = userId });
                }

                var sql = @"
                    INSERT INTO [Core].[Addresses]
                        (User_ID, Address_Line1, Address_Line2, City, Department, Zip_Code, Is_Default, Created_At)
                    VALUES
                        (@UserId, @AddressLine1, @AddressLine2, @City, @Department, @ZipCode, @IsDefault, GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newAddressId = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    UserId = userId,
                    request.AddressLine1,
                    request.AddressLine2,
                    request.City,
                    request.Department,
                    request.ZipCode,
                    request.IsDefault
                });

                return Created($"/api/customer/profile/addresses/{newAddressId}", new
                {
                    Message = "Dirección de envío agregada exitosamente.",
                    AddressId = newAddressId,
                    request.AddressLine1,
                    request.City,
                    request.Department
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "CreateAddress");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al crear dirección", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/customer/profile/addresses/{id}
        /// Actualiza una dirección de envío del comprador autenticado.
        /// </summary>
        [HttpPut("addresses/{id}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] CreateUpdateAddressRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                int userId = GetUserId();
                using var connection = _dapperContext.CreateConnection();

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                if (request.IsDefault)
                {
                    await connection.ExecuteAsync("UPDATE [Core].[Addresses] SET Is_Default = 0 WHERE User_ID = @UserId", new { UserId = userId });
                }

                var sql = @"
                    UPDATE [Core].[Addresses]
                    SET Address_Line1 = @AddressLine1,
                        Address_Line2 = @AddressLine2,
                        City = @City,
                        Department = @Department,
                        Zip_Code = @ZipCode,
                        Is_Default = @IsDefault
                    WHERE Address_ID = @AddressId AND User_ID = @UserId";

                int rowsAffected = await connection.ExecuteAsync(sql, new
                {
                    request.AddressLine1,
                    request.AddressLine2,
                    request.City,
                    request.Department,
                    request.ZipCode,
                    request.IsDefault,
                    AddressId = id,
                    UserId = userId
                });

                if (rowsAffected == 0)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Dirección no encontrada",
                        Detail = $"La dirección ID {id} no existe o no pertenece al usuario autenticado."
                    });
                }

                return Ok(new { Message = "Dirección actualizada exitosamente", AddressId = id });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "UpdateAddress");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al actualizar dirección", Detail = ex.Message });
            }
        }
    }
}
