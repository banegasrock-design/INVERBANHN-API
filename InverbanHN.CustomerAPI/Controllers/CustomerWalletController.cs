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
    [Route("api/customer/wallet")]
    [Authorize(Roles = "Customer,SuperAdmin")]
    public class CustomerWalletController : CustomerBaseController
    {
        private readonly DapperContext _dapperContext;

        public CustomerWalletController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/customer/wallet/balance
        /// Extrae User_ID del JWT y calcula el saldo neto disponible en tiempo real sumando Amount_LPS en [Sales].[Wallet_Transactions].
        /// </summary>
        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            try
            {
                int userId = GetUserId();
                using var connection = _dapperContext.CreateConnection();

                var sql = @"
                    SELECT 
                        @UserId AS UserId,
                        ISNULL(SUM(Amount_LPS), 0.00) AS AvailableBalanceLps,
                        ISNULL(SUM(CASE WHEN Amount_LPS > 0 THEN Amount_LPS ELSE 0 END), 0.00) AS TotalDepositedLps,
                        ISNULL(SUM(CASE WHEN Amount_LPS < 0 THEN ABS(Amount_LPS) ELSE 0 END), 0.00) AS TotalSpentLps,
                        ISNULL(MAX(Created_At), GETUTCDATE()) AS LastTransactionDate
                    FROM [Sales].[Wallet_Transactions]
                    WHERE User_ID = @UserId";

                var balance = await connection.QuerySingleOrDefaultAsync<WalletBalanceResponseDto>(sql, new { UserId = userId });

                return Ok(balance ?? new WalletBalanceResponseDto { UserId = userId });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetBalance");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al consultar saldo de Wallet", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/customer/wallet/history
        /// Retorna el estado de cuenta paginado del cliente (historial de créditos, débitos y reembolsos).
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            try
            {
                int userId = GetUserId();
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                var countSql = @"
                    SELECT COUNT(1) 
                    FROM [Sales].[Wallet_Transactions] 
                    WHERE User_ID = @UserId";

                var historySql = @"
                    SELECT 
                        Transaction_ID AS TransactionId,
                        User_ID AS UserId,
                        Amount_LPS AS AmountLps,
                        Transaction_Type AS TransactionType,
                        Description,
                        Reference_ID AS ReferenceId,
                        Created_At AS CreatedAt
                    FROM [Sales].[Wallet_Transactions]
                    WHERE User_ID = @UserId
                    ORDER BY Transaction_ID DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { UserId = userId });
                var items = await connection.QueryAsync<WalletTransactionItemDto>(historySql, new
                {
                    UserId = userId,
                    Offset = offset,
                    PageSize = pageSize
                });

                return Ok(new PagedResultDto<WalletTransactionItemDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetHistory");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener historial de Wallet", Detail = ex.Message });
            }
        }
    }
}
