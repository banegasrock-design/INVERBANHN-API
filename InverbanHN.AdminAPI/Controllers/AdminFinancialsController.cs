using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.AdminAPI.DTOs;
using InverbanHN.Shared.Data;
using InverbanHN.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InverbanHN.AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/financials")]
    [Authorize(Roles = "SuperAdmin")]
    public class AdminFinancialsController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;
        private readonly IEmailService _emailService;

        public AdminFinancialsController(DapperContext dapperContext, IEmailService emailService)
        {
            _dapperContext = dapperContext;
            _emailService = emailService;
        }

        /// <summary>
        /// GET /api/admin/financials/overview
        /// KPIs globales de la plataforma: Total vendido históricamente (LPS), Total Comisiones InverbanHN (5%), Total Retenciones ISR (1%).
        /// </summary>
        [HttpGet("overview")]
        public async Task<IActionResult> GetFinancialsOverview()
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();

                var sql = @"
                    SELECT 
                        ISNULL(SUM(TotalAmount), 0.00) AS TotalGrossSalesLps,
                        ISNULL(SUM(TotalAmount * 0.05), 0.00) AS TotalPlatformCommissionsLps,
                        ISNULL(SUM(TotalAmount * 0.01), 0.00) AS TotalIsrWithholdingsLps,
                        COUNT(1) AS TotalCompletedOrders
                    FROM [dbo].[SubOrders]
                    WHERE Status IN ('Entregado', 'Confirmado', 'Completado')";

                var stats = await connection.QuerySingleOrDefaultAsync<FinancialsOverviewResponseDto>(sql);

                var payoutsPaid = await connection.ExecuteScalarAsync<decimal>(@"
                    SELECT ISNULL(SUM(Requested_Amount_LPS), 0.00) 
                    FROM [Sales].[Payout_Requests] 
                    WHERE Status = 'Paid'");

                var payoutsPending = await connection.ExecuteScalarAsync<decimal>(@"
                    SELECT ISNULL(SUM(Requested_Amount_LPS), 0.00) 
                    FROM [Sales].[Payout_Requests] 
                    WHERE Status IN ('Pending', 'Processing')");

                var activeStores = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(1) FROM [Core].[Stores] WHERE ISNULL(Is_Active, 1) = 1");

                stats.TotalPayoutsPaidLps = payoutsPaid;
                stats.PendingPayoutsLps = payoutsPending;
                stats.ActiveStoresCount = activeStores;

                return Ok(stats);
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetFinancialsOverview");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al calcular finanzas globales", Detail = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/admin/financials/payouts
        /// Lista las solicitudes de retiro ('Payout_Requests') de vendedores en estado 'Pending'.
        /// </summary>
        [HttpGet("payouts")]
        public async Task<IActionResult> GetPendingPayouts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            try
            {
                int offset = (pageNumber - 1) * pageSize;
                using var connection = _dapperContext.CreateConnection();

                var countSql = @"SELECT COUNT(1) FROM [Sales].[Payout_Requests] WHERE Status = 'Pending'";

                var payoutsSql = @"
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
                    WHERE Status = 'Pending'
                    ORDER BY Payout_ID ASC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                var totalCount = await connection.ExecuteScalarAsync<int>(countSql);
                var items = await connection.QueryAsync<dynamic>(payoutsSql, new { Offset = offset, PageSize = pageSize });

                return Ok(new PagedResultDto<dynamic>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "GetPendingPayouts");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al listar retiros pendientes", Detail = ex.Message });
            }
        }

        /// <summary>
        /// PATCH /api/admin/financials/payouts/{payoutId}/approve
        /// Aprueba el retiro cambiando estado a 'Paid', registra las notas administrativas y notifica por correo al vendedor.
        /// Auditoría: sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPatch("payouts/{payoutId}/approve")]
        public async Task<IActionResult> ApprovePayout(int payoutId, [FromBody] ApprovePayoutRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                using var connection = _dapperContext.CreateConnection();

                var payout = await connection.QuerySingleOrDefaultAsync<(int PayoutId, int StoreId, decimal Amount, string BankName, string Status)>(@"
                    SELECT Payout_ID AS PayoutId, Store_ID AS StoreId, Requested_Amount_LPS AS Amount, Bank_Name AS BankName, Status
                    FROM [Sales].[Payout_Requests]
                    WHERE Payout_ID = @PayoutId",
                    new { PayoutId = payoutId });

                if (payout.PayoutId <= 0)
                {
                    return NotFound(new ProblemDetails { Title = "Retiro no encontrado", Detail = $"No existe la solicitud de retiro ID {payoutId}." });
                }

                // Auditoría antes de escribir
                await SetAuditContextAsync(connection);

                var updateSql = @"
                    UPDATE [Sales].[Payout_Requests]
                    SET Status = 'Paid',
                        Admin_Notes = @AdminNotes,
                        Processed_At = GETUTCDATE()
                    WHERE Payout_ID = @PayoutId";

                int rows = await connection.ExecuteAsync(updateSql, new { AdminNotes = request.AdminNotes.Trim(), PayoutId = payoutId });

                if (rows > 0)
                {
                    await WriteAuditLogAsync(connection, "Sales.Payout_Requests", "APPROVE_PAYOUT", $"Retiro ID {payoutId} aprobado por L. {payout.Amount:N2}. Ref: {request.AdminNotes}");

                    // Notificación por correo al vendedor
                    var vendorOwner = await connection.QuerySingleOrDefaultAsync<(string Email, string StoreName)>(@"
                        SELECT COALESCE(S.Support_Email, S.Email, U.Email) AS Email, S.Store_Name AS StoreName
                        FROM [Core].[Stores] S
                        LEFT JOIN [Core].[Users] U ON U.User_ID = S.Owner_User_ID
                        WHERE S.Store_ID = @StoreId",
                        new { StoreId = payout.StoreId });

                    if (!string.IsNullOrEmpty(vendorOwner.Email))
                    {
                        string emailHtml = $@"
                            <div style='font-family: Arial, sans-serif; padding: 20px;'>
                                <h2 style='color: #E20074;'>Liquidación Aprobada - InverbanHN</h2>
                                <p>Estimado equipo de <strong>{vendorOwner.StoreName}</strong>,</p>
                                <p>Le informamos que su solicitud de retiro por la cantidad de <strong style='color: #059669;'>L. {payout.Amount:N2}</strong> a su cuenta de {payout.BankName} ha sido procesada y pagada exitosamente.</p>
                                <div style='background-color: #F3F4F6; padding: 15px; border-left: 4px solid #E20074; margin: 20px 0;'>
                                    <p style='margin:0;'><strong>Referencia Bancaria / Notas:</strong> {request.AdminNotes}</p>
                                </div>
                                <p>Gracias por formar parte del marketplace de Honduras.</p>
                            </div>";

                        _ = Task.Run(() => _emailService.SendEmailAsync(vendorOwner.Email, $"Liquidación Pagada - L. {payout.Amount:N2}", emailHtml));
                    }
                }

                return Ok(new
                {
                    Message = "Solicitud de liquidación aprobada y transferida con éxito.",
                    PayoutId = payoutId,
                    Status = "Paid",
                    AdminNotes = request.AdminNotes.Trim()
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "ApprovePayout");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al aprobar liquidación", Detail = ex.Message });
            }
        }
    }
}
