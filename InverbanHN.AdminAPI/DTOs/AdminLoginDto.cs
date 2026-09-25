using System;
using System.ComponentModel.DataAnnotations;

namespace InverbanHN.AdminAPI.DTOs
{
    public class AdminLoginDto
    {
        public string? Email { get; set; }
        public string? email { get; set; }
        public string? Password { get; set; }
        public string? password { get; set; }
    }

    public class AdminLoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "SuperAdmin";
        public DateTime Expiration { get; set; }
    }
}
