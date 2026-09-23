using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Infrastructure.Data.Contexts;
using System.Linq;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MarketplaceDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(MarketplaceDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest loginInfo)
    {
        Console.WriteLine($"[AUTH] Login attempt for: {loginInfo.Username}");

        if (string.IsNullOrEmpty(loginInfo.Username) || string.IsNullOrEmpty(loginInfo.Password))
        {
            return BadRequest(new { Message = "Username and Password are required." });
        }

        var user = _context.Users.FirstOrDefault(u => u.Email == loginInfo.Username && u.PasswordHash == loginInfo.Password);

        if (user == null && loginInfo.Username == "armando.banegas")
        {
            user = _context.Users.FirstOrDefault(u => u.Email == "armando.banegas@inverbanhn.com" && u.PasswordHash == loginInfo.Password);
        }

        if (user == null)
        {
            Console.WriteLine($"[AUTH] Login failed for: {loginInfo.Username}");
            return Unauthorized(new { Message = "Credenciales inválidas." });
        }

        if (!user.IsActive)
            return Forbid("La cuenta está desactivada.");

        var token = GenerateJwtToken(user);
        Console.WriteLine($"[AUTH] Login successful for: {user.Email}");

        return Ok(new
        {
            Token = token,
            User = new
            {
                user.Id,
                user.FullName,
                user.Email,
                Role = user.RoleName
            }
        });
    }

    private string GenerateJwtToken(Domain.Entities.Core.User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "ARMANDO_BANEGAS_SUPER_SECRET_SECURITY_KEY_2026"));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.RoleName),
            new Claim("Id", user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "INVERBANHN_API",
            audience: _configuration["Jwt:Audience"] ?? "INVERBANHN_CLIENTS",
            claims: claims,
            expires: DateTime.Now.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
