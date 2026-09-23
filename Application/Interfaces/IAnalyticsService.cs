using Application.DTOs.Analytics;
using Application.Common;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IAnalyticsService
{
    Task<Result<GiftCardLiabilityDto>> GetGiftCardLiabilityAsync();
    Task<Result<PendingInvoicesSummaryDto>> GetPendingInvoicesSummaryAsync();
    Task<Result<MarketingReachDto>> GetMarketingReachStatsAsync();
}
