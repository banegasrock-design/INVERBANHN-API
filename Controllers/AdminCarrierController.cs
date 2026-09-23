using System;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using Application.DTOs.Carrier;
using Application.DTOs.Reports;
using Infrastructure.Data.Contexts;

namespace INVERBANHN.Controllers;

/// <summary>
/// Gestión de Proveedores Logísticos (Carriers) y Tarifas de Envío.
/// </summary>
[ApiController]
[Route("api/admin/carriers")]
public class AdminCarrierController : ControllerBase
{
    private readonly DapperContext _dapperContext;

    public AdminCarrierController(DapperContext dapperContext)
    {
        _dapperContext = dapperContext;
    }

    // ══════════════════════════════════════════════════════════════
    //  CARRIERS CRUD
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista todos los carriers activos.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllCarriers()
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = "SELECT Carrier_ID, Carrier_Name, Offers_COD, COD_Fee_Percentage, Is_Active FROM [Logistics].[Carriers] ORDER BY Carrier_Name";
        var carriers = await connection.QueryAsync(sql);
        return Ok(carriers);
    }

    /// <summary>
    /// Crea un nuevo proveedor logístico. Si ofrece COD, se debe especificar el porcentaje de comisión.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCarrier([FromBody] CreateCarrierRequest request)
    {
        using var connection = _dapperContext.CreateConnection();

        try
        {
            var sql = @"
                INSERT INTO [Logistics].[Carriers] 
                    (Carrier_Name, API_Integration_Key, Offers_COD, COD_Fee_Percentage, Is_Active)
                VALUES 
                    (@Carrier_Name, @API_Integration_Key, @Offers_COD, @COD_Fee_Percentage, 1);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var carrierId = await connection.QuerySingleAsync<int>(sql, new
            {
                Carrier_Name = request.CarrierName,
                API_Integration_Key = request.ApiIntegrationKey,
                Offers_COD = request.OffersCOD,
                COD_Fee_Percentage = request.OffersCOD ? request.CodFeePercentage : null
            });

            return CreatedAtAction(nameof(GetAllCarriers), new { carrierId }, new
            {
                Message = "Carrier creado exitosamente.",
                CarrierId = carrierId,
                CarrierName = request.CarrierName,
                OffersCOD = request.OffersCOD
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new { Error = $"Error de base de datos: {ex.Message}" });
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  SHIPPING RATES
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Lista las tarifas de envío de un carrier específico.
    /// </summary>
    /// <param name="carrierId">ID del carrier.</param>
    [HttpGet("{carrierId}/rates")]
    public async Task<IActionResult> GetRatesByCarrier(int carrierId)
    {
        using var connection = _dapperContext.CreateConnection();
        var sql = @"
            SELECT Rate_ID, Origin_City, Destination_City, Cost_To_Ecommerce, Price_To_Customer
            FROM [Logistics].[Shipping_Rates] 
            WHERE Carrier_ID = @CarrierId
            ORDER BY Origin_City, Destination_City";

        var rates = await connection.QueryAsync(sql, new { CarrierId = carrierId });
        return Ok(rates);
    }

    /// <summary>
    /// Crea una nueva tarifa de envío por ruta (ciudad origen → destino).
    /// Los montos están en Lempiras (LPS) con 2 decimales de precisión.
    /// </summary>
    [HttpPost("rates")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateShippingRate([FromBody] CreateShippingRateRequest request)
    {
        using var connection = _dapperContext.CreateConnection();

        try
        {
            var sql = @"
                INSERT INTO [Logistics].[Shipping_Rates] 
                    (Carrier_ID, Origin_City, Destination_City, Cost_To_Ecommerce, Price_To_Customer)
                VALUES 
                    (@Carrier_ID, @Origin_City, @Destination_City, @Cost_To_Ecommerce, @Price_To_Customer);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var rateId = await connection.QuerySingleAsync<int>(sql, new
            {
                Carrier_ID = request.CarrierId,
                Origin_City = request.OriginCity,
                Destination_City = request.DestinationCity,
                Cost_To_Ecommerce = request.CostToEcommerce,
                Price_To_Customer = request.PriceToCustomer
            });

            return CreatedAtAction(nameof(GetRatesByCarrier), new { carrierId = request.CarrierId }, new
            {
                Message = "Tarifa creada exitosamente.",
                RateId = rateId,
                Route = $"{request.OriginCity} → {request.DestinationCity}",
                CostToEcommerce = $"L. {request.CostToEcommerce:N2}",
                PriceToCustomer = $"L. {request.PriceToCustomer:N2}",
                Margin = $"L. {(request.PriceToCustomer - request.CostToEcommerce):N2}"
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new { Error = $"Error de base de datos: {ex.Message}" });
        }
    }

    /// <summary>
    /// Carga masiva de tarifas de envío para un carrier.
    /// Ideal para configurar todas las rutas de una paquetera de golpe.
    /// </summary>
    [HttpPost("rates/bulk")]
    public async Task<IActionResult> BulkCreateShippingRates([FromBody] List<CreateShippingRateRequest> rates)
    {
        if (rates == null || rates.Count == 0)
            return BadRequest(new { Error = "Debe enviar al menos una tarifa." });

        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var sql = @"
                INSERT INTO [Logistics].[Shipping_Rates] 
                    (Carrier_ID, Origin_City, Destination_City, Cost_To_Ecommerce, Price_To_Customer)
                VALUES 
                    (@Carrier_ID, @Origin_City, @Destination_City, @Cost_To_Ecommerce, @Price_To_Customer)";

            foreach (var rate in rates)
            {
                await connection.ExecuteAsync(sql, new
                {
                    Carrier_ID = rate.CarrierId,
                    Origin_City = rate.OriginCity,
                    Destination_City = rate.DestinationCity,
                    Cost_To_Ecommerce = rate.CostToEcommerce,
                    Price_To_Customer = rate.PriceToCustomer
                }, transaction);
            }

            transaction.Commit();

            return Ok(new
            {
                Message = $"{rates.Count} tarifas creadas exitosamente.",
                TotalRates = rates.Count
            });
        }
        catch (SqlException ex)
        {
            transaction.Rollback();
            return BadRequest(new { Error = $"Error en carga masiva: {ex.Message}. Ninguna tarifa fue guardada." });
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  REPORTE: LIQUIDACIÓN POR CARRIER
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Consulta la vista de exportación bancaria filtrada por Carrier_ID.
    /// Permite al administrador ver cuánto dinero debe transferir a cada paquetera.
    /// Nota: La vista actual no contiene Carrier_ID. Se consulta la tabla de pagos
    /// relacionada con el carrier para calcular la liquidación pendiente.
    /// </summary>
    /// <param name="carrierId">ID del carrier para filtrar.</param>
    [HttpGet("{carrierId}/settlement")]
    public async Task<IActionResult> GetCarrierSettlement(int carrierId)
    {
        using var connection = _dapperContext.CreateConnection();

        // Verificar que el carrier existe
        var carrier = await connection.QuerySingleOrDefaultAsync(
            "SELECT Carrier_ID, Carrier_Name FROM [Logistics].[Carriers] WHERE Carrier_ID = @CarrierId",
            new { CarrierId = carrierId });

        if (carrier == null)
            return NotFound(new { Error = "El carrier no existe." });

        // Calcular liquidación pendiente basado en los envíos completados
        var settlementSql = @"
            SELECT 
                c.Carrier_Name AS Beneficiario,
                COUNT(so.Sub_Order_ID) AS Total_Envios,
                SUM(sr.Cost_To_Ecommerce) AS Monto_Total_Pendiente,
                MIN(so.Created_At) AS Desde,
                MAX(so.Created_At) AS Hasta
            FROM [Logistics].[Carriers] c
            INNER JOIN [Logistics].[Shipping_Rates] sr ON sr.Carrier_ID = c.Carrier_ID
            LEFT JOIN [Sales].[Sub_Orders] so ON so.Carrier_ID = c.Carrier_ID
            WHERE c.Carrier_ID = @CarrierId
            GROUP BY c.Carrier_Name";

        var settlement = await connection.QuerySingleOrDefaultAsync(settlementSql, new { CarrierId = carrierId });

        // También devolver la vista de banca como referencia (exportación general)
        var bankExports = await connection.QueryAsync<BankExportDto>(
            "SELECT * FROM [Sales].[v_Export_Banca_En_Linea]");

        return Ok(new
        {
            Carrier = carrier,
            Settlement = settlement,
            BankExportReference = bankExports
        });
    }
}
