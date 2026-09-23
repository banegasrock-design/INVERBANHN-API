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
    [Route("api/vendor/customers")]
    [Authorize(Roles = "StoreAdmin")]
    public class VendorCustomersController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;

        public VendorCustomersController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/vendor/customers (Listado de Clientes Locales)
        /// Devuelve una lista paginada de los clientes globales ([Core].[Users]) que han realizado 
        /// al menos una compra en la tienda vinculada al Store_ID del JWT.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            try
            {
                int storeId = GetStoreId();
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                // Conteo de clientes únicos que han comprado en la tienda
                var countSql = @"
                    SELECT COUNT(DISTINCT Customer_User_ID)
                    FROM (
                        SELECT COALESCE(CustomerId, Customer_User_ID, User_ID) AS Customer_User_ID
                        FROM [dbo].[SubOrders]
                        WHERE StoreId = @StoreId AND COALESCE(CustomerId, Customer_User_ID, User_ID) IS NOT NULL
                    ) CustomerSubOrders";

                // Consulta paginada uniendo [Core].[Users] o fallback con información de sub-órdenes
                var itemsSql = @"
                    SELECT 
                        C.UserId,
                        COALESCE(U.Full_Name, U.Nombre, C.GuestName, 'Cliente ' + CAST(C.UserId AS NVARCHAR)) AS FullName,
                        COALESCE(U.Email, C.GuestEmail, 'sin_correo@inverban.hn') AS Email,
                        COALESCE(U.Phone, U.Telefono, U.Teléfono) AS Phone,
                        C.TotalOrdersInStore,
                        C.TotalSpentInStore,
                        C.LastOrderDate
                    FROM (
                        SELECT 
                            COALESCE(CustomerId, Customer_User_ID, User_ID) AS UserId,
                            COUNT(1) AS TotalOrdersInStore,
                            ISNULL(SUM(TotalAmount), 0.00) AS TotalSpentInStore,
                            MAX(CreatedAt) AS LastOrderDate,
                            MAX(CustomerName) AS GuestName,
                            MAX(CustomerEmail) AS GuestEmail
                        FROM [dbo].[SubOrders]
                        WHERE StoreId = @StoreId AND COALESCE(CustomerId, Customer_User_ID, User_ID) IS NOT NULL
                        GROUP BY COALESCE(CustomerId, Customer_User_ID, User_ID)
                    ) C
                    LEFT JOIN [Core].[Users] U ON U.User_ID = C.UserId OR U.Id = C.UserId
                    WHERE (@Search IS NULL 
                        OR U.Full_Name LIKE '%' + @Search + '%' 
                        OR U.Email LIKE '%' + @Search + '%'
                        OR C.GuestName LIKE '%' + @Search + '%')
                    ORDER BY C.LastOrderDate DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { StoreId = storeId });
                var items = await connection.QueryAsync<VendorCustomerListItemDto>(itemsSql, new
                {
                    StoreId = storeId,
                    Search = search,
                    Offset = offset,
                    PageSize = pageSize
                });

                return Ok(new PagedResultDto<VendorCustomerListItemDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetCustomers");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener clientes", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/vendor/customers/{userId} (Perfil de Cliente en la Tienda)
        /// SEGURIDAD CRÍTICA: Verifica primeramente si existe al menos una SubOrder vinculando al userId con el Store_ID.
        /// Si el cliente NUNCA le ha comprado a este vendedor, retorna HTTP 404 Not Found para proteger la privacidad global del marketplace.
        /// Excluye explícitamente passwords, hash, tokens y saldos globales de Wallet.
        /// </summary>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetCustomerDetail(int userId)
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Validación de Seguridad Crítica: Verificar relación comerciante-cliente
                var hasPurchasedInStore = await connection.ExecuteScalarAsync<bool>(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 
                        FROM [dbo].[SubOrders] 
                        WHERE StoreId = @StoreId 
                          AND COALESCE(CustomerId, Customer_User_ID, User_ID) = @UserId
                    ) THEN 1 ELSE 0 END",
                    new { StoreId = storeId, UserId = userId });

                if (!hasPurchasedInStore)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Cliente no encontrado",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"El usuario {userId} no tiene registros de compra asociados a su comercio. Acceso denegado por políticas de privacidad global."
                    });
                }

                // Consulta de perfil básico y métricas exclusivas de la tienda actual
                var sql = @"
                    SELECT 
                        @UserId AS UserId,
                        COALESCE(U.Full_Name, U.Nombre, M.GuestName, 'Cliente ' + CAST(@UserId AS NVARCHAR)) AS FullName,
                        COALESCE(U.Email, M.GuestEmail, 'sin_correo@inverban.hn') AS Email,
                        COALESCE(U.Phone, U.Telefono, U.Teléfono) AS Phone,
                        U.Created_At AS MemberSince,
                        @StoreId AS StoreId,
                        M.TotalOrdersInStore,
                        M.TotalSpentInStore,
                        M.FirstOrderDateInStore,
                        M.LastOrderDateInStore
                    FROM (
                        SELECT 
                            COUNT(1) AS TotalOrdersInStore,
                            ISNULL(SUM(TotalAmount), 0.00) AS TotalSpentInStore,
                            MIN(CreatedAt) AS FirstOrderDateInStore,
                            MAX(CreatedAt) AS LastOrderDateInStore,
                            MAX(CustomerName) AS GuestName,
                            MAX(CustomerEmail) AS GuestEmail
                        FROM [dbo].[SubOrders]
                        WHERE StoreId = @StoreId 
                          AND COALESCE(CustomerId, Customer_User_ID, User_ID) = @UserId
                    ) M
                    LEFT JOIN [Core].[Users] U ON U.User_ID = @UserId OR U.Id = @UserId";

                var customerDetail = await connection.QuerySingleOrDefaultAsync<VendorCustomerDetailDto>(sql, new
                {
                    UserId = userId,
                    StoreId = storeId
                });

                if (customerDetail == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Detalle de cliente no disponible",
                        Detail = $"No fue posible recuperar la información del usuario {userId}."
                    });
                }

                return Ok(customerDetail);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetCustomerDetail");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener detalle del cliente", Detail = ex.Message });
            }
        }
    }
}
