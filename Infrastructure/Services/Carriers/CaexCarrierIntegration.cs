using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Domain.Entities.Core;
using Domain.Entities.Sales;
using Domain.Entities.Logistics;
using Domain.Entities.Marketing;
using Infrastructure.Data.Contexts;

namespace Infrastructure.Services.Carriers;

public class CaexCarrierIntegration : ICarrierIntegration
{
    private readonly MarketplaceDbContext _context;
    
    // Asumimos que CAEX tiene el CarrierId = 1
    public int CarrierId => 1;

    public CaexCarrierIntegration(MarketplaceDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetQuoteAsync(string originCity, string destinationCity)
    {
        var rate = await _context.ShippingRates
            .FirstOrDefaultAsync(r => r.CarrierId == CarrierId 
                                      && r.OriginCity == originCity 
                                      && r.DestinationCity == destinationCity);

        if (rate == null)
            throw new Exception("No hay cobertura de CAEX para esta ruta.");

        return rate.BaseRate;
    }

    public Task<string> GenerateShippingGuideAsync(SubOrder order)
    {
        // Simulación de generación de guía
        string guideNumber = "CAEX-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
        string instructions = "";

        if (order.RequiresCarrierCollection)
        {
            instructions = $"[ATENCIÓN MOTORISTA CAEX] COBRO CONTRA ENTREGA (COD). Cobrar exactamente {order.AmountToCollect} Lempiras al entregar.";
        }

        string guideDetails = $@"
        ========================================
        GUÍA CAEX: {guideNumber}
        ORDEN: {order.OrderNumber}
        INSTRUCCIONES: {instructions}
        ========================================";

        return Task.FromResult(guideDetails);
    }
}

