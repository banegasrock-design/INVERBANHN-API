using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InverbanHN.AdminAPI.DTOs
{
    // ----------------------------------------------------
    // Admin Stores DTOs
    // ----------------------------------------------------
    public class AdminStoreListItemDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string? Rtn { get; set; }
        public string InventoryMode { get; set; } = "Tienda";
        public string BillingType { get; set; } = "Managed";
        public string? Cai { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateAdminStoreRequestDto
    {
        [Required(ErrorMessage = "El nombre de la tienda es obligatorio.")]
        [StringLength(150)]
        public string StoreName { get; set; } = string.Empty;

        [StringLength(14, MinimumLength = 14, ErrorMessage = "El RTN debe contener exactamente 14 dígitos.")]
        public string? Rtn { get; set; }

        public string InventoryMode { get; set; } = "Tienda"; // Tienda / Ecommerce
        public string BillingType { get; set; } = "Managed"; // Managed / Autoimpresor
    }

    public class UpdateAdminStoreStatusRequestDto
    {
        [Required]
        public bool IsActive { get; set; }
    }

    public class ConfigureAdminStoreSarRequestDto
    {
        [Required(ErrorMessage = "El número de CAI es obligatorio.")]
        public string Cai { get; set; } = string.Empty;

        [Required(ErrorMessage = "El rango inicial es obligatorio.")]
        public long RangeStart { get; set; }

        [Required(ErrorMessage = "El rango final es obligatorio.")]
        public long RangeEnd { get; set; }

        [Required(ErrorMessage = "La fecha límite de emisión es obligatoria.")]
        public DateTime CaiExpiryDate { get; set; }
    }

    // ----------------------------------------------------
    // Financials & Payouts DTOs
    // ----------------------------------------------------
    public class FinancialsOverviewResponseDto
    {
        public decimal TotalGrossSalesLps { get; set; }
        public decimal TotalPlatformCommissionsLps { get; set; } // 5%
        public decimal TotalIsrWithholdingsLps { get; set; }     // 1%
        public decimal TotalPayoutsPaidLps { get; set; }
        public decimal PendingPayoutsLps { get; set; }
        public int ActiveStoresCount { get; set; }
        public int TotalCompletedOrders { get; set; }
    }

    public class ApprovePayoutRequestDto
    {
        [Required(ErrorMessage = "Las notas administrativas son obligatorias (Ej. Número de referencia bancaria).")]
        public string AdminNotes { get; set; } = string.Empty;
    }

    // ----------------------------------------------------
    // User & Store Assignment DTOs
    // ----------------------------------------------------
    public class AdminUserListItemDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AssignStoreRoleRequestDto
    {
        [Required(ErrorMessage = "El StoreId es obligatorio.")]
        public int StoreId { get; set; }

        public string Role { get; set; } = "StoreAdmin"; // StoreAdmin / StoreOperator
    }

    // ----------------------------------------------------
    // Security & Audit DTOs
    // ----------------------------------------------------
    public class ApiKeyListItemDto
    {
        public int KeyId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GenerateApiKeyRequestDto
    {
        [Required(ErrorMessage = "El nombre de la aplicación compradora/integración es obligatorio.")]
        [StringLength(100)]
        public string AppName { get; set; } = string.Empty;
    }

    public class GenerateApiKeyResponseDto
    {
        public int KeyId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string PlainTextApiKey { get; set; } = string.Empty; // MOSTRADO UNA SOLA VEZ
        public string Prefix { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Warning { get; set; } = "Guarde esta API Key en un lugar seguro. No se podrá volver a mostrar.";
    }

    public class AuditLogItemDto
    {
        public int LogId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public int OperatorUserId { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PagedResultDto<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 10));
    }
}
