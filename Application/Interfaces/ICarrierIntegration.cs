using System.Threading.Tasks;
using Domain.Entities.Core;
using Domain.Entities.Sales;
using Domain.Entities.Logistics;
using Domain.Entities.Marketing;

namespace Application.Interfaces;

public interface ICarrierIntegration
{
    int CarrierId { get; }
    Task<decimal> GetQuoteAsync(string originCity, string destinationCity);
    Task<string> GenerateShippingGuideAsync(SubOrder order);
}

