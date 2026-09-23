using System;
using System.ComponentModel.DataAnnotations;

namespace InverbanHN.VendorAPI.DTOs
{
    // ----------------------------------------------------
    // Store Profile DTOs
    // ----------------------------------------------------
    public class StoreProfileResponseDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string? Rtn { get; set; }
        public string? InventoryMode { get; set; }
        public string? BillingType { get; set; }
        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public string? SupportEmail { get; set; }
        public string? Address { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateStoreProfileRequestDto
    {
        [Required(ErrorMessage = "El nombre de la tienda es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede exceder los 150 caracteres.")]
        public string StoreName { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [EmailAddress(ErrorMessage = "Formato de correo electrónico de soporte inválido.")]
        public string? SupportEmail { get; set; }

        public string? Address { get; set; }
        public string? Description { get; set; }
    }

    // ----------------------------------------------------
    // Account DTOs
    // ----------------------------------------------------
    public class ChangePasswordRequestDto
    {
        [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "La nueva contraseña debe tener al menos 6 caracteres.")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class StoreSarSettingsDto
    {
        public int StoreId { get; set; }
        public string? Rtn { get; set; }
        public string? Cai { get; set; }
        public long? RangeStart { get; set; }
        public long? RangeEnd { get; set; }
        public DateTime? CaiExpiryDate { get; set; }
        public string? BillingType { get; set; }
    }

    public class UpdateStoreSarSettingsRequestDto
    {
        [StringLength(14, MinimumLength = 14, ErrorMessage = "El RTN debe tener exactamente 14 dígitos.")]
        public string? Rtn { get; set; }

        public string? Cai { get; set; }
        public long? RangeStart { get; set; }
        public long? RangeEnd { get; set; }
        public DateTime? CaiExpiryDate { get; set; }
        
        [StringLength(50)]
        public string? BillingType { get; set; }
    }

    // ----------------------------------------------------
    // Catalog DTOs
    // ----------------------------------------------------
    public class CatalogItemResponseDto
    {
        public int ProductId { get; set; }
        public int StoreId { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int? Stock { get; set; }
        public string ProductType { get; set; } = "Producto"; // Producto / Servicio
        public decimal? WeightKg { get; set; }
        public decimal? LengthCm { get; set; }
        public decimal? WidthCm { get; set; }
        public decimal? HeightCm { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateUpdateCatalogItemRequestDto
    {
        [Required(ErrorMessage = "El SKU es obligatorio.")]
        [StringLength(50)]
        public string SKU { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del artículo es obligatorio.")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(0.01, 9999999.99, ErrorMessage = "El precio debe ser un valor positivo.")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "El tipo de producto es obligatorio ('Producto' o 'Servicio').")]
        public string ProductType { get; set; } = "Producto"; // "Producto" | "Servicio"

        // Para Servicios, Stock y dimensiones pueden ser NULL
        public int? Stock { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? LengthCm { get; set; }
        public decimal? WidthCm { get; set; }
        public decimal? HeightCm { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class AdjustStockRequestDto
    {
        [Required(ErrorMessage = "El ID del producto es obligatorio.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "El nuevo valor de stock es obligatorio.")]
        [Range(0, 1000000, ErrorMessage = "El stock no puede ser negativo.")]
        public int NewStock { get; set; }
    }

    public class PagedResultDto<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 10));
    }

    // ----------------------------------------------------
    // Marketing DTOs
    // ----------------------------------------------------
    public class CreateProductOfferRequestDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(0.01, 9999999.99)]
        public decimal OfferPrice { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
    }

    public class ProductOfferResponseDto
    {
        public int OfferId { get; set; }
        public int ProductId { get; set; }
        public int StoreId { get; set; }
        public decimal OfferPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class CouponResponseDto
    {
        public int CouponId { get; set; }
        public int StoreId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DiscountType { get; set; } = "Fixed"; // "Fixed" | "Percentage"
        public decimal DiscountValue { get; set; }
        public decimal MinPurchaseLps { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? UsageLimit { get; set; }
        public bool IsStackable { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateCouponRequestDto
    {
        [Required(ErrorMessage = "El código de cupón es obligatorio.")]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de descuento es obligatorio ('Fixed' o 'Percentage').")]
        public string DiscountType { get; set; } = "Fixed"; // Fixed / Percentage

        [Required]
        [Range(0.01, 999999.99, ErrorMessage = "El valor del descuento debe ser mayor a 0.")]
        public decimal DiscountValue { get; set; }

        public decimal MinPurchaseLps { get; set; } = 0.00m;

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public int? UsageLimit { get; set; }

        public bool IsStackable { get; set; } = false;
    }

    public class UpdateCouponRequestDto
    {
        [Required(ErrorMessage = "El valor del descuento es obligatorio.")]
        [Range(0.01, 999999.99)]
        public decimal DiscountValue { get; set; }

        public decimal MinPurchaseLps { get; set; } = 0.00m;

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public int? UsageLimit { get; set; }

        public bool IsStackable { get; set; } = false;
    }

    // ----------------------------------------------------
    // Orders DTOs
    // ----------------------------------------------------
    public class SubOrderResponseDto
    {
        public int SubOrderId { get; set; }
        public int StoreId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateOrderStatusRequestDto
    {
        [Required(ErrorMessage = "El estado es obligatorio (ej. 'En Preparación', 'Enviado').")]
        public string Status { get; set; } = string.Empty;

        public string? TrackingNumber { get; set; }
    }

    public class CancelOrderRequestDto
    {
        [Required(ErrorMessage = "La razón de cancelación es obligatoria.")]
        public string Reason { get; set; } = string.Empty;
    }

    // ----------------------------------------------------
    // Analytics DTOs
    // ----------------------------------------------------
    public class AnalyticsSummaryResponseDto
    {
        public int StoreId { get; set; }
        public decimal GrossSalesLps { get; set; }
        public decimal PlatformCommissionsLps { get; set; }
        public decimal IsrWithholdingsLps { get; set; }
        public decimal NetSalesLps { get; set; }
        public int TotalCompletedOrders { get; set; }
    }

    // ----------------------------------------------------
    // Vendor Customers DTOs (Global User, Local View)
    // ----------------------------------------------------
    public class VendorCustomerListItemDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int TotalOrdersInStore { get; set; }
        public decimal TotalSpentInStore { get; set; }
        public DateTime? LastOrderDate { get; set; }
    }

    public class VendorCustomerDetailDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public DateTime? MemberSince { get; set; }

        // Métricas exclusivas de la tienda
        public int StoreId { get; set; }
        public int TotalOrdersInStore { get; set; }
        public decimal TotalSpentInStore { get; set; }
        public DateTime? FirstOrderDateInStore { get; set; }
        public DateTime? LastOrderDateInStore { get; set; }
    }

    // ----------------------------------------------------
    // Payout (Financial Settlements) DTOs
    // ----------------------------------------------------
    public class PayoutBalanceResponseDto
    {
        public int StoreId { get; set; }
        public decimal TotalEarned { get; set; }
        public decimal PendingClearance { get; set; }
        public decimal PlatformCommissions { get; set; }
        public decimal IsrWithholdings { get; set; }
        public decimal TotalPayoutsClaimed { get; set; }
        public decimal AvailableBalance { get; set; }
    }

    public class RequestPayoutRequestDto
    {
        [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
        [Range(500.00, 1000000.00, ErrorMessage = "El monto mínimo de retiro es L. 500.00.")]
        public decimal RequestedAmount { get; set; }

        [Required(ErrorMessage = "El nombre del banco es obligatorio.")]
        [StringLength(150)]
        public string BankName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número de cuenta bancaria es obligatorio.")]
        [StringLength(100)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del titular de la cuenta es obligatorio.")]
        [StringLength(150)]
        public string AccountHolderName { get; set; } = string.Empty;
    }

    public class PayoutRequestResponseDto
    {
        public int PayoutId { get; set; }
        public int StoreId { get; set; }
        public decimal RequestedAmountLps { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string AccountHolderName { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Processing, Paid, Rejected
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    // ----------------------------------------------------
    // Shipping Rules & Logistics DTOs
    // ----------------------------------------------------
    public class ShippingRuleResponseDto
    {
        public int RuleId { get; set; }
        public int StoreId { get; set; }
        public string ZoneName { get; set; } = "Nacional";
        public decimal StandardCostLps { get; set; }
        public decimal? FreeShippingMinLps { get; set; }
        public int EstimatedDays { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateUpdateShippingRuleRequestDto
    {
        [Required(ErrorMessage = "El nombre de la zona es obligatorio (Ej: San Pedro Sula, Tegucigalpa, Nacional).")]
        [StringLength(100)]
        public string ZoneName { get; set; } = "Nacional";

        [Required(ErrorMessage = "El costo estándar de envío es obligatorio.")]
        [Range(0.00, 10000.00, ErrorMessage = "El costo estándar debe ser mayor o igual a 0.")]
        public decimal StandardCostLps { get; set; } = 120.00m;

        [Range(0.00, 1000000.00, ErrorMessage = "El monto mínimo para envío gratis debe ser positivo.")]
        public decimal? FreeShippingMinLps { get; set; }

        [Range(1, 60, ErrorMessage = "Los días estimados de entrega deben estar entre 1 y 60.")]
        public int EstimatedDays { get; set; } = 2;

        public bool IsActive { get; set; } = true;
    }

    public class CalculateShippingRequestDto
    {
        [Required(ErrorMessage = "El StoreId es obligatorio.")]
        public int StoreId { get; set; }

        public string? ZoneName { get; set; } // Opcional (Ej: San Pedro Sula)

        [Required(ErrorMessage = "El subtotal del carrito es obligatorio para calcular el envío gratis.")]
        [Range(0.00, 10000000.00)]
        public decimal CartSubtotalLps { get; set; }
    }

    public class CalculateShippingResponseDto
    {
        public int StoreId { get; set; }
        public string ZoneName { get; set; } = "Nacional";
        public decimal FinalShippingCostLps { get; set; }
        public bool IsFreeShipping { get; set; }
        public decimal? FreeShippingMinLps { get; set; }
        public decimal StandardCostLps { get; set; }
        public int EstimatedDays { get; set; }
    }
}
