using System;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Store;

public class CreateStoreRequest
{
    // ── Datos Legales ──

    /// <summary>
    /// Nombre legal de la tienda.
    /// </summary>
    /// <example>Ferretería El Constructor SPS</example>
    [Required]
    public string StoreName { get; set; } = string.Empty;

    /// <summary>
    /// RTN del contribuyente (14 dígitos numéricos, formato SAR Honduras).
    /// </summary>
    /// <example>08019000123456</example>
    [Required]
    public string RTN { get; set; } = string.Empty;

    /// <summary>
    /// Dirección fiscal de la tienda.
    /// </summary>
    /// <example>Col. Los Andes, 3ra Calle, San Pedro Sula, Cortés</example>
    [Required]
    public string Address { get; set; } = string.Empty;

    // ── Configuración Operativa ──

    /// <summary>
    /// Modo de inventario: "Tienda" (stock propio) o "Ecommerce" (bodega central).
    /// </summary>
    /// <example>Ecommerce</example>
    [Required]
    public string InventoryMode { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de facturación: "Autoimpresor" (factura propia con CAI) o "Managed" (facturado por la plataforma).
    /// </summary>
    /// <example>Autoimpresor</example>
    [Required]
    public string BillingType { get; set; } = string.Empty;

    // ── Configuración SAR (Solo para Autoimpresores) ──

    /// <summary>
    /// Código de Autorización de Impresión (CAI) emitido por el SAR.
    /// Solo requerido si BillingType es "Autoimpresor".
    /// Formato: XXXXXX-XXXXXX-XXXXXX-XXXXXX-XXXXXX-XX
    /// </summary>
    /// <example>A1B2C3-D4E5F6-G7H8I9-J0K1L2-M3N4O5-P6</example>
    public string? CAI { get; set; }

    /// <summary>
    /// Número inicial del rango autorizado por el SAR.
    /// </summary>
    /// <example>1</example>
    public long? RangeStart { get; set; }

    /// <summary>
    /// Número final del rango autorizado por el SAR.
    /// </summary>
    /// <example>500</example>
    public long? RangeEnd { get; set; }

    /// <summary>
    /// Fecha de vencimiento del CAI (formato ISO 8601).
    /// </summary>
    /// <example>2025-12-31</example>
    public DateTime? CaiExpiryDate { get; set; }

    /// <summary>
    /// ID del usuario propietario de la tienda (vendedor).
    /// </summary>
    /// <example>1</example>
    public int? OwnerUserId { get; set; }
}
