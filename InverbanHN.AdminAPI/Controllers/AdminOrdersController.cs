using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InverbanHN.AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/orders")]
    [Authorize(Roles = "SuperAdmin")]
    public class AdminOrdersController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;

        public AdminOrdersController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/admin/orders
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrders([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var sql = @"
                    SELECT 
                        SubOrder_ID AS id,
                        Order_Number AS orderNumber,
                        TotalAmount AS total_Amount_LPS,
                        Status AS global_Status_Name,
                        Customer_ID AS customerId,
                        Store_ID AS storeId,
                        Carrier_ID AS carrierId,
                        Tracking_Number AS trackingNumber
                    FROM [dbo].[SubOrders]
                    ORDER BY SubOrder_ID DESC";

                var orders = await connection.QueryAsync(sql);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Title = "Error al listar órdenes", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/admin/orders/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var orderSql = @"
                    SELECT 
                        SubOrder_ID AS id,
                        Order_Number AS orderNumber,
                        TotalAmount AS total_Amount_LPS,
                        Status AS global_Status_Name,
                        Customer_ID AS customerId,
                        Store_ID AS storeId,
                        Carrier_ID AS carrierId,
                        Tracking_Number AS trackingNumber
                    FROM [dbo].[SubOrders]
                    WHERE SubOrder_ID = @Id";

                var order = await connection.QuerySingleOrDefaultAsync(orderSql, new { Id = id });
                if (order == null) return NotFound(new { Message = "Orden no encontrada." });

                var itemsSql = @"
                    SELECT 
                        Item_Name AS productName,
                        Product_Type AS productType,
                        Product_Type AS product_Type,
                        Quantity AS quantity,
                        Unit_Price AS price
                    FROM [dbo].[SubOrderItems]
                    WHERE SubOrder_ID = @Id";

                var items = await connection.QueryAsync(itemsSql, new { Id = id });

                return Ok(new { order, items });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Title = "Error al obtener detalle de orden", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/orders/cancel-refund
        /// </summary>
        [HttpPost("cancel-refund")]
        public async Task<IActionResult> CancelAndRefund([FromBody] CancelOrderRequest request)
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var sql = "UPDATE [dbo].[SubOrders] SET Status = 'Cancelado' WHERE SubOrder_ID = @OrderId";
                int rows = await connection.ExecuteAsync(sql, new { OrderId = request.OrderId });

                if (rows == 0) return NotFound(new { Message = "Orden no encontrada." });

                return Ok(new { Message = "Orden cancelada y reembolsada exitosamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Title = "Error al cancelar orden", Detail = ex.Message });
            }
        }
    }

    public class CancelOrderRequest
    {
        public int OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
