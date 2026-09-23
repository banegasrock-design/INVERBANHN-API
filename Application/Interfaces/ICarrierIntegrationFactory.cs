using System.Collections.Generic;
using Application.Interfaces;

namespace Application.Interfaces;

public interface ICarrierIntegrationFactory
{
    ICarrierIntegration GetCarrierIntegration(int carrierId);
}
