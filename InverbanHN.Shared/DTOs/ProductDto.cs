using System;

namespace InverbanHN.Shared.DTOs;

public class ProductDto
{
    public int ProductId { get; set; }
    public int StoreId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategorySlug { get; set; }
    public string? Brand { get; set; }
    public decimal WeightKg { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal LengthCm { get; set; }
    public string StatusName { get; set; } = "Activo";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal Price { get; set; }
    public decimal RegularPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

public class CreateProductRequest
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public string? Brand { get; set; }
    public decimal WeightKg { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
    public decimal LengthCm { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
}
