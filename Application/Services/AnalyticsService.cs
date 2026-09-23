using Application.DTOs.Analytics;
using Application.Interfaces;
using Application.Common;
using Infrastructure.Data.Contexts;
using Dapper;
using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Application.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly DapperContext _dapperContext;

    public AnalyticsService(DapperContext dapperContext)
    {
        _dapperContext = dapperContext;
    }

    public async Task<Result<GiftCardLiabilityDto>> GetGiftCardLiabilityAsync()
    {
        using var connection = _dapperContext.CreateConnection();
        try
        {
            // Usando la vista especificada por el usuario
            var sql = @"
                SELECT 
                    COUNT(*) as TotalPendingCards, 
                    ISNULL(SUM(Saldo_Pendiente), 0) as TotalLiabilityLps 
                FROM [Marketing].[v_Auditoria_GiftCards_Pendientes]";
            
            var stats = await connection.QuerySingleAsync<GiftCardLiabilityDto>(sql);
            return Result<GiftCardLiabilityDto>.Success(stats);
        }
        catch (SqlException ex) when (ex.Number == 208) // Invalid object name (vista no existe)
        {
            return Result<GiftCardLiabilityDto>.Failure("La vista de auditoría de Gift Cards no ha sido desplegada en la base de datos.");
        }
        catch (Exception ex)
        {
            return Result<GiftCardLiabilityDto>.Failure($"Error al consultar pasivo de Gift Cards: {ex.Message}");
        }
    }

    public async Task<Result<PendingInvoicesSummaryDto>> GetPendingInvoicesSummaryAsync()
    {
        using var connection = _dapperContext.CreateConnection();
        try
        {
            // Usando la vista especificada por el usuario
            var sql = @"
                SELECT 
                    COUNT(*) as PendingCount, 
                    ISNULL(SUM(Monto_Factura), 0) as TotalAmountLps 
                FROM [Sales].[v_Facturas_Pendientes_Carga]";
            
            var stats = await connection.QuerySingleAsync<PendingInvoicesSummaryDto>(sql);
            return Result<PendingInvoicesSummaryDto>.Success(stats);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return Result<PendingInvoicesSummaryDto>.Failure("La vista de facturas pendientes de carga no ha sido desplegada en la base de datos.");
        }
        catch (Exception ex)
        {
            return Result<PendingInvoicesSummaryDto>.Failure($"Error al consultar facturas pendientes: {ex.Message}");
        }
    }

    public async Task<Result<MarketingReachDto>> GetMarketingReachStatsAsync()
    {
        using var connection = _dapperContext.CreateConnection();
        try
        {
            // Asumiendo la existencia de una tabla de preferencias de notificación
            // Si la tabla no existe, devolveremos un error controlado.
            var sql = @"
                DECLARE @TotalUsers INT = (SELECT COUNT(*) FROM [Core].[Users] WHERE Is_Active = 1);
                DECLARE @OptOuts INT = (
                    SELECT COUNT(DISTINCT User_ID) 
                    FROM [Core].[User_Notification_Preferences] 
                    WHERE Channel_Name = 'Marketing' AND Is_Enabled = 0
                );
                
                SELECT @TotalUsers as TotalUsers, @OptOuts as OptOutCount;";
            
            var stats = await connection.QuerySingleAsync<MarketingReachDto>(sql);
            return Result<MarketingReachDto>.Success(stats);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return Result<MarketingReachDto>.Failure("La tabla de preferencias de notificación [Core].[User_Notification_Preferences] no existe.");
        }
        catch (Exception ex)
        {
            return Result<MarketingReachDto>.Failure($"Error al consultar alcance de marketing: {ex.Message}");
        }
    }
}
