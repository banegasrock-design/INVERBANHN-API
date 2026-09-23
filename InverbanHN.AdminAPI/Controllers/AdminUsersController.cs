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
    [Route("api/admin/users")]
    [Authorize(Roles = "SuperAdmin")]
    public class AdminUsersController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;

        public AdminUsersController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/admin/users
        /// Búsqueda paginada de usuarios globales en [Core].[Users].
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUsers(
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
                    FROM [Core].[Users]
                    WHERE (@Search IS NULL OR Full_Name LIKE '%' + @Search + '%' OR Email LIKE '%' + @Search + '%')";

                var usersSql = @"
                    SELECT 
                        COALESCE(User_ID, Id) AS UserId,
                        COALESCE(Full_Name, Nombre, 'Usuario') AS FullName,
                        Email,
                        COALESCE(Phone, Telefono, Teléfono) AS Phone,
                        ISNULL(Role, 'Customer') AS Role,
                        ISNULL(Is_Active, 1) AS IsActive,
                        ISNULL(Created_At, GETUTCDATE()) AS CreatedAt
                    FROM [Core].[Users]
                    WHERE (@Search IS NULL OR Full_Name LIKE '%' + @Search + '%' OR Email LIKE '%' + @Search + '%')
                    ORDER BY User_ID DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { Search = search });
                var users = await connection.QueryAsync<AdminUserListItemDto>(usersSql, new
                {
                    Search = search,
                    Offset = offset,
                    PageSize = pageSize
                });

                return Ok(new PagedResultDto<AdminUserListItemDto>
                {
                    Items = users,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetUsers");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al listar usuarios", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/users/{userId}/assign-store
        /// Asigna a un usuario el rol de 'StoreAdmin' para una tienda específica en [Core].[User_Stores].
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost("{userId}/assign-store")]
        public async Task<IActionResult> AssignStoreRole(int userId, [FromBody] AssignStoreRoleRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                using var connection = _dapperContext.CreateConnection();

                // Verificar que el usuario existe
                var userExists = await connection.ExecuteScalarAsync<bool>(@"
                    SELECT CASE WHEN EXISTS (SELECT 1 FROM [Core].[Users] WHERE User_ID = @UserId OR Id = @UserId) THEN 1 ELSE 0 END",
                    new { UserId = userId });

                if (!userExists)
                {
                    return NotFound(new ProblemDetails { Title = "Usuario no encontrado", Detail = $"No existe el usuario con ID {userId}." });
                }

                // Verificar que la tienda existe
                var storeExists = await connection.ExecuteScalarAsync<bool>(@"
                    SELECT CASE WHEN EXISTS (SELECT 1 FROM [Core].[Stores] WHERE Store_ID = @StoreId) THEN 1 ELSE 0 END",
                    new { request.StoreId });

                if (!storeExists)
                {
                    return NotFound(new ProblemDetails { Title = "Tienda no encontrada", Detail = $"No existe la tienda con ID {request.StoreId}." });
                }

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                // Insertar asignación o actualizar rol existente
                var sql = @"
                    IF EXISTS (SELECT 1 FROM [Core].[User_Stores] WHERE User_ID = @UserId AND Store_ID = @StoreId)
                    BEGIN
                        UPDATE [Core].[User_Stores] SET Role = @Role WHERE User_ID = @UserId AND Store_ID = @StoreId;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Core].[User_Stores] (User_ID, Store_ID, Role, Assigned_At)
                        VALUES (@UserId, @StoreId, @Role, GETUTCDATE());
                    END";

                await connection.ExecuteAsync(sql, new
                {
                    UserId = userId,
                    StoreId = request.StoreId,
                    Role = request.Role
                });

                // Actualizar también el rol principal en [Core].[Users] si aplica
                await connection.ExecuteAsync("UPDATE [Core].[Users] SET Role = @Role WHERE User_ID = @UserId OR Id = @UserId",
                    new { Role = request.Role, UserId = userId });

                await WriteAuditLogAsync(connection, "Core.User_Stores", "ASSIGN_STORE", $"Usuario {userId} asignado como {request.Role} de Tienda {request.StoreId}.");

                return Ok(new
                {
                    Message = $"Rol '{request.Role}' asignado con éxito al usuario {userId} para la tienda {request.StoreId}.",
                    UserId = userId,
                    StoreId = request.StoreId,
                    Role = request.Role
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "AssignStoreRole");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al asignar rol de tienda", Detail = ex.Message });
            }
        }
    }
}
