using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Application.Common;
using Application.Interfaces;
using Infrastructure.Data.Contexts;
using Dapper;

namespace Application.Services;

public class TaxService : ITaxService
{
    private readonly MarketplaceDbContext _context;
    private readonly DapperContext _dapperContext;

    public TaxService(MarketplaceDbContext context, DapperContext dapperContext)
    {
        _context = context;
        _dapperContext = dapperContext;
    }

    public async Task<Result<bool>> ProcessOrderBillingAsync(Guid subOrderId)
    {
        // 1. Obtener la orden y la tienda (para revisar si tiene retenciones)
        var order = await _context.SubOrders
            .Include(o => o.Store)
            .FirstOrDefaultAsync(o => o.Id == subOrderId);

        if (order == null)
            return Result<bool>.Failure("La orden especificada no existe.");

        // 2. Abrir conexión Dapper con transacción explícita
        //    para garantizar atomicidad: si la retención falla, la facturación hace rollback.
        using var connection = _dapperContext.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 3. Ejecutar Facturación
            var billingParams = new DynamicParameters();
            billingParams.Add("@Sub_Order_ID", order.Id);

            await connection.ExecuteAsync(
                "[Sales].[usp_Procesar_Facturacion_Orden]",
                billingParams,
                transaction: transaction,
                commandType: CommandType.StoredProcedure
            );

            // 4. Ejecutar Retención Automática si aplica
            if (order.Store.HasIsrWithholding)
            {
                var withholdingParams = new DynamicParameters();
                withholdingParams.Add("@Store_ID", order.StoreId);
                withholdingParams.Add("@Sub_Order_ID", order.Id);

                await connection.ExecuteAsync(
                    "[Sales].[usp_Generar_Retencion_Automatica]",
                    withholdingParams,
                    transaction: transaction,
                    commandType: CommandType.StoredProcedure
                );
            }

            // 5. Si todo fue exitoso, confirmar la transacción
            transaction.Commit();

            return Result<bool>.Success(true);
        }
        catch (SqlException ex)
        {
            // Rollback automático para mantener la integridad financiera
            transaction.Rollback();

            if (ex.Number == 50060)
            {
                return Result<bool>.Failure($"Error de Facturación: {ex.Message} (Falta configurar rangos CAI).");
            }

            return Result<bool>.Failure($"Error en la base de datos: {ex.Message}");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return Result<bool>.Failure($"Error interno del servidor: {ex.Message}");
        }
    }
}
