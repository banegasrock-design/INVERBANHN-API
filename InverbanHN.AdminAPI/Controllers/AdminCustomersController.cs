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
    [Route("api/admin/customers")]
    [Authorize(Roles = "SuperAdmin")]
    public class AdminCustomersController : AdminBaseController
    {
        private readonly DapperContext _dapperContext;

        public AdminCustomersController(DapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// GET /api/admin/customers
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomers([FromQuery] string? search = null)
        {
            try
            {
                using var connection = _dapperContext.CreateConnection();
                var sql = @"
                    SELECT 
                        User_ID AS CustomerId,
                        Full_Name AS FullName,
                        Email AS Email,
                        ISNULL(Created_At, GETUTCDATE()) AS CreatedAt
                    FROM [Core].[Users]
                    WHERE (@Search IS NULL OR Full_Name LIKE '%' + @Search + '%' OR Email LIKE '%' + @Search + '%')
                    ORDER BY User_ID DESC";

                var customers = await connection.QueryAsync(sql, new { Search = search });
                return Ok(customers);
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al obtener clientes", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/admin/customers/{customerId}/reset-password
        /// </summary>
        [HttpPost("{customerId}/reset-password")]
        public async Task<IActionResult> ResetPassword(int customerId, [FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.NewPassword))
                return BadRequest(new { Message = "La nueva contraseña es requerida." });

            try
            {
                using var connection = _dapperContext.CreateConnection();
                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
                var passwordHash = hasher.HashPassword(this, request.NewPassword);

                var sql = "UPDATE [Core].[Users] SET Password_Hash = @Hash WHERE User_ID = @CustomerId";
                int rows = await connection.ExecuteAsync(sql, new { Hash = passwordHash, CustomerId = customerId });

                if (rows == 0) return NotFound(new { Message = "Cliente no encontrado." });

                return Ok(new { Message = "Contraseña restablecida exitosamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al restablecer contraseña", Detail = ex.Message });
            }
        }
    }

    public class ResetPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }
}
