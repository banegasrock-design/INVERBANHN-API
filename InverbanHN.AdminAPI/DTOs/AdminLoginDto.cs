using System;
using System.ComponentModel.DataAnnotations;

namespace InverbanHN.AdminAPI.DTOs
{
    public class AdminLoginDto
    {
        private string _email = string.Empty;
        private string _password = string.Empty;

        [Required(ErrorMessage = "El correo electrónico o usuario es requerido.")]
        public string Email 
        { 
            get => _email; 
            set => _email = value; 
        }

        public string email 
        { 
            get => _email; 
            set => _email = value; 
        }

        [Required(ErrorMessage = "La contraseña es requerida.")]
        public string Password 
        { 
            get => _password; 
            set => _password = value; 
        }

        public string password 
        { 
            get => _password; 
            set => _password = value; 
        }

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
