using System;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InverbanHN.AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/gift-cards")]
    [Authorize(Roles = "SuperAdmin")]
    public class AdminGiftCardsController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;

        public AdminGiftCardsController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/admin/gift-cards
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetGiftCards()
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var sql = @"
                    SELECT 
                        Card_ID AS id,
                        Card_Code AS code,
                        Initial_Balance AS amount,
                        CASE WHEN Status = 'Redeemed' THEN 1 ELSE 0 END AS isRedeemed,
                        Redeemed_At AS redeemedAt,
                        Redeemed_By_Wallet_ID AS redeemedByWalletId
                    FROM [Sales].[Gift_Cards]
                    ORDER BY Card_ID DESC";

                var cards = await connection.QueryAsync(sql);
                return Ok(cards);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Title = "Error al listar tarjetas de regalo", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/gift-cards
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateGiftCard([FromBody] CreateGiftCardRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest(new { Message = "El monto debe ser mayor a cero." });

            try
            {
                using var connection = _dapperContext.CreateConnection();
                string cardCode = !string.IsNullOrWhiteSpace(request.Code) 
                    ? request.Code.Trim().ToUpperInvariant() 
                    : $"GC-{Guid.NewGuid().ToString()[..8].ToUpper()}";

                var sql = @"
                    INSERT INTO [Sales].[Gift_Cards] (Card_Code, Initial_Balance, Current_Balance, Status, Created_At)
                    VALUES (@CardCode, @Amount, @Amount, 'Active', GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int cardId = await connection.ExecuteScalarAsync<int>(sql, new { CardCode = cardCode, Amount = request.Amount });

                return Ok(new
                {
                    Message = "Tarjeta de regalo creada con éxito.",
                    Id = cardId,
                    Code = cardCode,
                    Amount = request.Amount
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Title = "Error al crear tarjeta de regalo", Detail = ex.Message });
            }
        }
    }

    public class CreateGiftCardRequest
    {
        public string? Code { get; set; }
        public decimal Amount { get; set; }
    }
}
