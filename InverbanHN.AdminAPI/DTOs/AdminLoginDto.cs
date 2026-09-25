using System;
using System.ComponentModel.DataAnnotations;

namespace InverbanHN.AdminAPI.DTOs
{
    public class AdminLoginDto
    {
        [Required(ErrorMessage = "El correo electrónico o usuario es requerido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida.")]
        public string Password { get; set; } = string.Empty;
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
