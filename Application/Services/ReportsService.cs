using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using Application.Interfaces;
using Application.DTOs.Reports;
using Application.Common;
using Infrastructure.Data.Contexts;

namespace Application.Services;

public class ReportsService : IReportsService
{
    private readonly DapperContext _dapperContext;

    public ReportsService(DapperContext dapperContext)
    {
        _dapperContext = dapperContext;
    }

    public async Task<Result<IEnumerable<BankExportDto>>> GetBankExportsAsync()
    {
        try
        {
            using var connection = _dapperContext.CreateConnection();
            
            // Dapper automáticamente hace el mapeo por nombre de columna a propiedad del DTO
            var query = "SELECT * FROM [Sales].[v_Export_Banca_En_Linea]";
            
            var result = await connection.QueryAsync<BankExportDto>(query);
            
            return Result<IEnumerable<BankExportDto>>.Success(result);
        }
        catch (SqlException ex)
        {
            return Result<IEnumerable<BankExportDto>>.Failure($"Error al consultar la vista de Base de Datos: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<BankExportDto>>.Failure($"Error interno del servidor: {ex.Message}");
        }
    }
}
