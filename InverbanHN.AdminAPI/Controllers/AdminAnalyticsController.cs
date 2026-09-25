using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InverbanHN.AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/analytics")]
    [Authorize(Roles = "SuperAdmin")]
    public class AdminAnalyticsController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;

        public AdminAnalyticsController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/admin/analytics/gift-cards/liability
        /// </summary>
        [HttpGet("gift-cards/liability")]
        public async Task<IActionResult> GetGiftCardsLiability()
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var sql = @"
                    SELECT 
                        ISNULL(SUM(Current_Balance), 0.00) AS TotalLiabilityLps,
                        COUNT(1) AS PendingCardsCount
                    FROM [Sales].[Gift_Cards]
                    WHERE Status = 'Active' AND Current_Balance > 0";

                var result = await connection.QuerySingleOrDefaultAsync(sql);
                return Ok(result ?? new { TotalLiabilityLps = 0.00, PendingCardsCount = 0 });
            }
            catch (Exception ex)
            {
                return Ok(new { TotalLiabilityLps = 0.00, PendingCardsCount = 0 });
            }
        }

        /// <summary>
        /// GET /api/admin/analytics/billing/pending-upload
        /// </summary>
        [HttpGet("billing/pending-upload")]
        public async Task<IActionResult> GetPendingInvoices()
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var sql = @"
                    SELECT 
                        COUNT(1) AS TotalPendingInvoices,
                        ISNULL(SUM(TotalAmount), 0.00) AS TotalPendingAmount
                    FROM [dbo].[SubOrders]
                    WHERE InvoiceUrl IS NULL OR InvoiceUrl = ''";

                var result = await connection.QuerySingleOrDefaultAsync(sql);
                return Ok(result ?? new { TotalPendingInvoices = 0, TotalPendingAmount = 0.00 });
            }
            catch (Exception ex)
            {
                return Ok(new { TotalPendingInvoices = 0, TotalPendingAmount = 0.00 });
            }
        }

        /// <summary>
        /// GET /api/admin/analytics/notifications/marketing-reach
        /// </summary>
        [HttpGet("notifications/marketing-reach")]
        public async Task<IActionResult> GetMarketingReach()
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var totalUsers = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM [Core].[Users]");
                var activeSubscribers = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM [Core].[Users] WHERE ISNULL(Is_Active, 1) = 1");

                double percentage = totalUsers > 0 ? ((double)activeSubscribers / totalUsers) * 100 : 100.0;

                return Ok(new
                {
                    ReachPercentage = percentage,
                    ActiveSubscribers = activeSubscribers,
                    TotalEligibleUsers = totalUsers
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    ReachPercentage = 100.0,
                    ActiveSubscribers = 1,
                    TotalEligibleUsers = 1
                });
            }
        }
    }
}
