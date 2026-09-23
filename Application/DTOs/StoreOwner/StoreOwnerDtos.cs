using System;

namespace Application.DTOs.StoreOwner;

public class DashboardStatsDto
{
    public decimal GrossRevenueCurrentMonth { get; set; }
    public int PendingShipments { get; set; }
    public int CompletedShipments { get; set; }
    public int ActiveProducts { get; set; }
    public int CouponRedemptions { get; set; }
}

public class SalesTrendDto
{
    public string Date { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
}

public class CreateProductOfferRequest
{
    public int ProductId { get; set; }
    public decimal OfferPrice { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class CreateCouponRequest
{
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Fixed"; // "Fixed" or "Percent"
    public decimal DiscountValue { get; set; }
    public decimal MinimumPurchase { get; set; }
    public int LimitPerUser { get; set; } = 1;
    public bool IsStackable { get; set; }
}
