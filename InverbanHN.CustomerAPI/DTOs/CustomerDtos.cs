using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InverbanHN.CustomerAPI.DTOs
{
    // ----------------------------------------------------
    // Public Catalog DTOs
    // ----------------------------------------------------
    public class PublicProductListItemDto
    {
        public int ProductId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? OfferPrice { get; set; }
        public decimal EffectivePrice => OfferPrice.HasValue && OfferPrice > 0 ? OfferPrice.Value : BasePrice;
        public int? Stock { get; set; }
        public string ProductType { get; set; } = "Producto";
        public string? MainImageUrl { get; set; }
        public bool IsActive { get; set; }
    }

    public class PublicProductDetailDto : PublicProductListItemDto
    {
        public string? Description { get; set; }
        public string? StoreLogo { get; set; }
        public decimal? WeightKg { get; set; }
        public List<string> GalleryImages { get; set; } = new List<string>();
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
    // Customer Profile & Address DTOs
    // ----------------------------------------------------
    public class CustomerProfileResponseDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public DateTime MemberSince { get; set; }
        public List<CustomerAddressDto> Addresses { get; set; } = new List<CustomerAddressDto>();
    }

    public class CustomerAddressDto
    {
        public int AddressId { get; set; }
        public int UserId { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string? ZipCode { get; set; }
        public bool IsDefault { get; set; }
    }

    public class CreateUpdateAddressRequestDto
    {
        [Required(ErrorMessage = "La dirección de envío es obligatoria.")]
        [StringLength(250)]
        public string AddressLine1 { get; set; } = string.Empty;

        public string? AddressLine2 { get; set; }

        [Required(ErrorMessage = "La ciudad es obligatoria.")]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "El departamento es obligatorio (Ej: Cortés, Francisco Morazán).")]
        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        public string? ZipCode { get; set; }
        public bool IsDefault { get; set; } = false;
    }

    // ----------------------------------------------------
    // Checkout & Cart Preview DTOs
    // ----------------------------------------------------
    public class CartItemPreviewDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, 1000)]
        public int Quantity { get; set; }
    }

    public class CheckoutPreviewRequestDto
    {
        [Required(ErrorMessage = "La lista de artículos es obligatoria.")]
        public List<CartItemPreviewDto> Items { get; set; } = new List<CartItemPreviewDto>();

        public string? CouponCode { get; set; }
    }

    public class CartItemCalculatedDto
    {
        public int ProductId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPriceDb { get; set; } // PRECIO CALCULADO RECALCULADO DE BD
        public decimal LineSubtotal => Math.Round(UnitPriceDb * Quantity, 2);
    }

    public class CheckoutPreviewResponseDto
    {
        public List<CartItemCalculatedDto> Items { get; set; } = new List<CartItemCalculatedDto>();
        public decimal SubtotalLps { get; set; }
        public decimal CouponDiscountLps { get; set; }
        public string? AppliedCouponCode { get; set; }
        public decimal ShippingCostLps { get; set; }
        public decimal TotalPayableLps { get; set; }
        public bool PriceSecurityValidated { get; set; } = true;
    }

    public class PayCheckoutRequestDto
    {
        public string? PixelPayToken { get; set; } // Opcional si el monto total se cubre 100% con Wallet

        public bool UseWalletBalance { get; set; } = false;

        public List<CartItemPreviewDto> Items { get; set; } = new List<CartItemPreviewDto>();

        public string? CouponCode { get; set; }

        [Required(ErrorMessage = "La dirección de entrega es obligatoria.")]
        public string ShippingAddress { get; set; } = string.Empty;

        public string? CustomerNotes { get; set; }
    }

    // ----------------------------------------------------
    // Auth DTOs
    // ----------------------------------------------------
    public class RegisterCustomerRequestDto
    {
        [Required(ErrorMessage = "El nombre completo es obligatorio.")]
        [StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido.")]
        public string Email { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Password { get; set; } = string.Empty;
    }

    public class CustomerLoginRequestDto
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Password { get; set; } = string.Empty;
    }

    public class CustomerLoginResponseDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Customer";
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
    }

    public class ForgotPasswordRequestDto
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    // ----------------------------------------------------
    // Wallet DTOs
    // ----------------------------------------------------
    public class WalletBalanceResponseDto
    {
        public int UserId { get; set; }
        public decimal AvailableBalanceLps { get; set; }
        public decimal TotalDepositedLps { get; set; }
        public decimal TotalSpentLps { get; set; }
        public DateTime LastTransactionDate { get; set; }
    }

    public class WalletTransactionItemDto
    {
        public int TransactionId { get; set; }
        public int UserId { get; set; }
        public decimal AmountLps { get; set; }
        public string TransactionType { get; set; } = string.Empty; // Deposit, Refund, Purchase, Adjustment
        public string? Description { get; set; }
        public string? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ----------------------------------------------------
    // Customer Orders History DTOs
    // ----------------------------------------------------
    public class CustomerOrderHeaderDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmountLps { get; set; }
        public string OverallStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int SubOrdersCount { get; set; }
    }

    public class CustomerOrderDetailDto : CustomerOrderHeaderDto
    {
        public string ShippingAddress { get; set; } = string.Empty;
        public List<CustomerSubOrderDetailDto> SubOrders { get; set; } = new List<CustomerSubOrderDetailDto>();
    }

    public class CustomerSubOrderDetailDto
    {
        public int SubOrderId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public decimal SubTotalLps { get; set; }
        public string Status { get; set; } = string.Empty; // Confirmado, En Preparación, Enviado, Completado
        public string? TrackingNumber { get; set; }
    }
}
