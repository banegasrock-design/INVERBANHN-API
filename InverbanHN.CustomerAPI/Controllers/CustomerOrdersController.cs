using System;
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
    [Route("api/customer/orders")]
    [Authorize(Roles = "Customer,SuperAdmin")]
    public class CustomerOrdersController : CustomerBaseController
    {
        private readonly DapperContext _dapperContext;

        public CustomerOrdersController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/customer/orders
        /// Lista el historial de compras del comprador autenticado.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyOrders([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            try
            {
                int userId = GetUserId();
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                var countSql = @"
                    SELECT COUNT(1) 
                    FROM [dbo].[SubOrders] 
                    WHERE CustomerId = @CustomerId";

                var ordersSql = @"
                    SELECT 
                        Id AS OrderId,
                        OrderNumber,
                        TotalAmount AS TotalAmountLps,
                        Status AS OverallStatus,
                        ISNULL(CreatedAt, GETUTCDATE()) AS CreatedAt,
                        1 AS SubOrdersCount
                    FROM [dbo].[SubOrders]
                    WHERE CustomerId = @CustomerId
                    ORDER BY Id DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { CustomerId = userId });
                var orders = await connection.QueryAsync<CustomerOrderHeaderDto>(ordersSql, new
                {
                    CustomerId = userId,
                    Offset = offset,
                    PageSize = pageSize
                });

                return Ok(new PagedResultDto<CustomerOrderHeaderDto>
                {
                    Items = orders,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetMyOrders");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener historial de compras", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/customer/orders/{id}
        /// Detalle completo de una orden específica con el estado logístico de sus Sub-Órdenes y número de guía.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderDetail(int id)
        {
            try
            {
                int userId = GetUserId();
                using var connection = _dapperContext.CreateConnection();

                var headerSql = @"
                    SELECT 
                        SO.Id AS OrderId,
                        SO.OrderNumber,
                        SO.TotalAmount AS TotalAmountLps,
                        SO.Status AS OverallStatus,
                        ISNULL(SO.CreatedAt, GETUTCDATE()) AS CreatedAt,
                        1 AS SubOrdersCount,
                        COALESCE(U.Address, 'Dirección de envío principal') AS ShippingAddress
                    FROM [dbo].[SubOrders] SO
                    LEFT JOIN [Core].[Users] U ON U.User_ID = SO.CustomerId
                    WHERE SO.Id = @OrderId AND SO.CustomerId = @CustomerId";

                var orderDetail = await connection.QuerySingleOrDefaultAsync<CustomerOrderDetailDto>(headerSql, new
                {
                    OrderId = id,
                    CustomerId = userId
                });

                if (orderDetail == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Orden no encontrada",
                        Status = StatusCodes.Status404NotFound,
                        Detail = $"La orden ID {id} no existe o no pertenece a su cuenta de usuario."
                    });
                }

                var subOrdersSql = @"
                    SELECT 
                        SO.Id AS SubOrderId,
                        SO.StoreId AS StoreId,
                        S.Store_Name AS StoreName,
                        SO.TotalAmount AS SubTotalLps,
                        SO.Status,
                        SO.TrackingNumber
                    FROM [dbo].[SubOrders] SO
                    INNER JOIN [Core].[Stores] S ON SO.StoreId = S.Store_ID
                    WHERE SO.Id = @OrderId AND SO.CustomerId = @CustomerId";

                var subOrders = await connection.QueryAsync<CustomerSubOrderDetailDto>(subOrdersSql, new
                {
                    OrderId = id,
                    CustomerId = userId
                });

                orderDetail.SubOrders = subOrders.AsList();

                return Ok(orderDetail);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetOrderDetail");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener detalle de la orden", Detail = ex.Message });
            }
        }
    }
}
