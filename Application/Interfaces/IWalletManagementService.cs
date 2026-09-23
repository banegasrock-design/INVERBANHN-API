using System.Threading.Tasks;
using Application.DTOs.Wallet;
using Application.Common;

namespace Application.Interfaces;

public interface IWalletManagementService
{
    /// <summary>
    /// Canjea una Gift Card llamando al SP [Marketing].[usp_Canjear_Gift_Card_Seguro].
    /// Retorna el nuevo saldo de la billetera.
    /// </summary>
    Task<Result<decimal>> RedeemGiftCardAsync(int customerId, string giftCardCode, string ipAddress);

    Task<MixedPaymentResponse> ProcessMixedPaymentAsync(int customerId, MixedPaymentRequest request);
}
