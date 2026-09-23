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

public class CargoExpresoIntegration : ICarrierIntegration
{
    private readonly MarketplaceDbContext _context;

    // Asumimos que Cargo Expreso tiene el CarrierId = 2
    public int CarrierId => 2;

    public CargoExpresoIntegration(MarketplaceDbContext context)
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
            throw new Exception("No hay cobertura de Cargo Expreso para esta ruta.");

        return rate.BaseRate;
    }

    public Task<string> GenerateShippingGuideAsync(SubOrder order)
    {
        // Simulación de generación de guía
        string guideNumber = "CEX-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
        string instructions = "Entrega Estándar";

        if (order.RequiresCarrierCollection)
        {
            instructions = $"SERVICIO COD SOLICITADO. RECAUDAR: L. {order.AmountToCollect}";
        }

        string guideDetails = $@"
        ****************************************
        CARGO EXPRESO - GUÍA: {guideNumber}
        REF ORDEN: {order.OrderNumber}
        OBSERVACIONES: {instructions}
        ****************************************";

        return Task.FromResult(guideDetails);
    }
}

