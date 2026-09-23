using System.Threading.Tasks;

namespace Application.Interfaces;

public interface INotificationService
{
    Task NotifyOrderStatusChangedAsync(int customerId, string orderNumber, string newStatus);
    Task NotifyPaymentConfirmedAsync(int customerId, string orderNumber);
    Task NotifyWalletBalanceUpdatedAsync(int customerId, decimal newBalance);
}
