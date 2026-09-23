using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Dapper;
using Application.Interfaces;
using Application.DTOs.Wallet;
using Application.Common;
using Domain.Entities.Sales;
using Infrastructure.Data.Contexts;

namespace Application.Services;

public class WalletManagementService : IWalletManagementService
{
    private readonly MarketplaceDbContext _context;
    private readonly DapperContext _dapperContext;

    public WalletManagementService(MarketplaceDbContext context, DapperContext dapperContext)
    {
        _context = context;
        _dapperContext = dapperContext;
    }

    public async Task<Result<decimal>> RedeemGiftCardAsync(int customerId, string giftCardCode, string ipAddress)
    {
        try
        {
            using var connection = _dapperContext.CreateConnection();

            var parameters = new DynamicParameters();
            parameters.Add("@Customer_ID", customerId);
            parameters.Add("@Gift_Card_Code", giftCardCode);
            parameters.Add("@IP_Address", ipAddress);
            parameters.Add("@Nuevo_Saldo", dbType: DbType.Decimal, direction: ParameterDirection.Output, precision: 18, scale: 2);

            await connection.ExecuteAsync(
                "[Marketing].[usp_Canjear_Gift_Card_Seguro]",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            var nuevoSaldo = parameters.Get<decimal>("@Nuevo_Saldo");

            return Result<decimal>.Success(nuevoSaldo);
        }
        catch (SqlException ex)
        {
            // Los errores de seguridad del SP (ej. demasiados intentos) vienen como SqlException
            return Result<decimal>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<decimal>.Failure($"Error interno del servidor: {ex.Message}");
        }
    }

    public async Task<MixedPaymentResponse> ProcessMixedPaymentAsync(int customerId, MixedPaymentRequest request)
    {
        if (request.TotalOrder <= 0)
            throw new Exception("El monto de la orden debe ser mayor a 0.");

        var response = new MixedPaymentResponse
        {
            TotalOrder = request.TotalOrder,
            WalletAmountUsed = 0,
            DiferenciaPendiente = request.TotalOrder
        };

        if (!request.UseWallet)
        {
            return response;
        }

        // 1. Buscar la Wallet del cliente
        var wallet = await _context.CustomerWallets
            .FirstOrDefaultAsync(w => w.CustomerId == customerId);

        if (wallet == null || wallet.Balance <= 0)
        {
            return response; // No hay saldo para descontar
        }

        // 2. Lógica de Pago Mixto
        if (wallet.Balance >= request.TotalOrder)
        {
            response.WalletAmountUsed = request.TotalOrder;
            response.DiferenciaPendiente = 0;
        }
        else
        {
            response.WalletAmountUsed = wallet.Balance;
            response.DiferenciaPendiente = request.TotalOrder - wallet.Balance;
        }

        // 3. Descontar saldo
        wallet.Balance -= response.WalletAmountUsed;

        // 4. Registrar transacción
        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = -response.WalletAmountUsed, // Monto negativo porque es débito
            TransactionType = "MixedPayment",
            ReferenceId = request.OrderId,
            CreatedAt = DateTime.UtcNow
        };
        _context.WalletTransactions.Add(transaction);

        // 5. Guardar cambios manejando concurrencia
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new Exception("Conflicto de concurrencia al realizar el cobro. El saldo fue modificado por otra transacción. Por favor, reintente.", ex);
        }

        return response;
    }
}
