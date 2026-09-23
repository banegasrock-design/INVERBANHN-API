using System;
using System.Collections.Generic;
using System.Linq;
using Application.Interfaces;

namespace Application.Services;

public class CarrierIntegrationFactory : ICarrierIntegrationFactory
{
    private readonly IEnumerable<ICarrierIntegration> _carrierIntegrations;

    public CarrierIntegrationFactory(IEnumerable<ICarrierIntegration> carrierIntegrations)
    {
        _carrierIntegrations = carrierIntegrations;
    }

    public ICarrierIntegration GetCarrierIntegration(int carrierId)
    {
        var integration = _carrierIntegrations.FirstOrDefault(c => c.CarrierId == carrierId);
        
        if (integration == null)
        {
            throw new NotSupportedException($"El Carrier con ID {carrierId} no tiene una integración configurada.");
        }

        return integration;
    }
}
