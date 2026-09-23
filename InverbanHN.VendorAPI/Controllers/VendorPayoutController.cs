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
    [Route("api/vendor/payouts")]
    [Authorize(Roles = "StoreAdmin")]
    public class VendorPayoutController : VendorBaseController
    {
        private readonly DapperContext _dapperContext;

        public VendorPayoutController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/vendor/payouts/balance
        /// Calcula el saldo real disponible del Store_ID en tiempo real.
        /// FÓRMULA: (Ventas Brutas de Sub_Orders 'Entregado'/'Completado') - 5% Comisión - 1% ISR - (Monto Retiros 'Pending'/'Processing'/'Paid').
        /// </summary>
        [HttpGet("balance")]
        public async Task<IActionResult> GetPayoutBalance()
        {
            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // 1. Total ventas brutas de órdenes completadas
                var totalEarned = await connection.ExecuteScalarAsync<decimal>(@"
                    SELECT ISNULL(SUM(TotalAmount), 0.00)
                    FROM [dbo].[SubOrders]
                    WHERE StoreId = @StoreId AND (Status = 'Entregado' OR Status = 'Completado')",
                    new { StoreId = storeId });

                // 2. Ventas en proceso de compensación / no entregadas
                var pendingClearance = await connection.ExecuteScalarAsync<decimal>(@"
                    SELECT ISNULL(SUM(TotalAmount), 0.00)
                    FROM [dbo].[SubOrders]
                    WHERE StoreId = @StoreId AND Status IN ('Confirmado', 'En Preparación', 'Enviado')",
                    new { StoreId = storeId });

                // 3. Comisiones de plataforma (5%) e ISR Retenciones (1%)
                decimal platformCommissions = Math.Round(totalEarned * 0.05m, 2);
                decimal isrWithholdings = Math.Round(totalEarned * 0.01m, 2);

                // 4. Suma de retiros solicitados / procesados / pagados
                var totalPayoutsClaimed = await connection.ExecuteScalarAsync<decimal>(@"
                    SELECT ISNULL(SUM(Requested_Amount_LPS), 0.00)
                    FROM [Sales].[Payout_Requests]
                    WHERE Store_ID = @StoreId AND Status IN ('Pending', 'Processing', 'Paid')",
                    new { StoreId = storeId });

                // Saldo disponible para retiro
                decimal availableBalance = totalEarned - platformCommissions - isrWithholdings - totalPayoutsClaimed;
                if (availableBalance < 0) availableBalance = 0.00m;

                return Ok(new PagedPayoutBalanceDto
                {
                    StoreId = storeId,
                    TotalEarned = totalEarned,
                    PendingClearance = pendingClearance,
                    PlatformCommissions = platformCommissions,
                    IsrWithholdings = isrWithholdings,
                    TotalPayoutsClaimed = totalPayoutsClaimed,
                    AvailableBalance = availableBalance
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetPayoutBalance");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al calcular saldo disponible", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/vendor/payouts/request
        /// Solicita una nueva liquidación de fondos a cuenta bancaria.
        /// REGLA DE NEGOCIO CRÍTICA:
        /// 1. Monto mínimo L. 500.00.
        /// 2. Monto debe ser MENOR O IGUAL al AvailableBalance en tiempo real.
        /// 3. Inyecta auditoría sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost("request")]
        public async Task<IActionResult> RequestPayout([FromBody] RequestPayoutRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (request.RequestedAmount < 500.00m)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Monto Mínimo No Alcanzado",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "El monto mínimo permitido para solicitar una liquidación es de L. 500.00."
                });
            }

            try
            {
                int storeId = GetStoreId();
                using var connection = _dapperContext.CreateConnection();

                // Recalcula el saldo disponible en tiempo real
                var totalEarned = await connection.ExecuteScalarAsync<decimal>(@"
                    SELECT ISNULL(SUM(TotalAmount), 0.00)
                    FROM [dbo].[SubOrders]
                    WHERE StoreId = @StoreId AND (Status = 'Entregado' OR Status = 'Completado')",
                    new { StoreId = storeId });

                decimal platformCommissions = Math.Round(totalEarned * 0.05m, 2);
                decimal isrWithholdings = Math.Round(totalEarned * 0.01m, 2);

                var totalPayoutsClaimed = await connection.ExecuteScalarAsync<decimal>(@"
                    SELECT ISNULL(SUM(Requested_Amount_LPS), 0.00)
                    FROM [Sales].[Payout_Requests]
                    WHERE Store_ID = @StoreId AND Status IN ('Pending', 'Processing', 'Paid')",
                    new { StoreId = storeId });

                decimal realTimeAvailableBalance = totalEarned - platformCommissions - isrWithholdings - totalPayoutsClaimed;

                if (request.RequestedAmount > realTimeAvailableBalance)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Fondos Insuficientes",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"El monto solicitado (L. {request.RequestedAmount:N2}) supera su saldo disponible actual (L. {realTimeAvailableBalance:N2})."
                    });
                }

                // Auditoría: sp_set_session_context 'UsuarioID'
                await SetAuditContextAsync(connection);

                var insertSql = @"
                    INSERT INTO [Sales].[Payout_Requests]
                        (Store_ID, Requested_Amount_LPS, Bank_Name, Bank_Account_Number, Account_Holder_Name, Status, Created_At)
                    VALUES
                        (@StoreId, @RequestedAmount, @BankName, @BankAccountNumber, @AccountHolderName, 'Pending', GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int newPayoutId = await connection.ExecuteScalarAsync<int>(insertSql, new
                {
                    StoreId = storeId,
                    request.RequestedAmount,
                    BankName = request.BankName.Trim(),
                    BankAccountNumber = request.BankAccountNumber.Trim(),
                    AccountHolderName = request.AccountHolderName.Trim()
                });

                return Created($"/api/vendor/payouts/history", new
                {
                    Message = "Solicitud de liquidación registrada con éxito y pendiente de procesamiento.",
                    PayoutId = newPayoutId,
                    StoreId = storeId,
                    RequestedAmount = request.RequestedAmount,
                    Status = "Pending"
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "RequestPayout");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al procesar solicitud de liquidación", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/vendor/payouts/history
        /// Historial de solicitudes de retiro paginado para la tienda autenticada.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetPayoutHistory(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            try
            {
                int storeId = GetStoreId();
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                var countSql = @"
                    SELECT COUNT(1) 
                    FROM [Sales].[Payout_Requests]
                    WHERE Store_ID = @StoreId
                      AND (@Status IS NULL OR Status = @Status)";

                var historySql = @"
                    SELECT 
                        Payout_ID AS PayoutId,
                        Store_ID AS StoreId,
                        Requested_Amount_LPS AS RequestedAmountLps,
                        Bank_Name AS BankName,
                        Bank_Account_Number AS BankAccountNumber,
                        Account_Holder_Name AS AccountHolderName,
                        Status,
                        Admin_Notes AS AdminNotes,
                        Created_At AS CreatedAt,
                        Processed_At AS ProcessedAt
                    FROM [Sales].[Payout_Requests]
                    WHERE Store_ID = @StoreId
                      AND (@Status IS NULL OR Status = @Status)
                    ORDER BY Payout_ID DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { StoreId = storeId, Status = status });
                var items = await connection.QueryAsync<PayoutRequestResponseDto>(historySql, new
                {
                    StoreId = storeId,
                    Status = status,
                    Offset = offset,
                    PageSize = pageSize
                });

                return Ok(new PagedResultDto<PayoutRequestResponseDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetPayoutHistory");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener historial de liquidaciones", Detail = ex.Message });
            }
        }
    }

    public class PagedPayoutBalanceDto : PayoutBalanceResponseDto { }
}
