using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Application.Interfaces;
using Infrastructure.Data.Contexts;

namespace Infrastructure.Repositories;

public class SqlStoredProcedureRepository : ISqlStoredProcedureRepository
{
    private readonly DapperContext _context;

    public SqlStoredProcedureRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<int> ExecuteAsync(string spName, DynamicParameters parameters)
    {
        using var connection = _context.CreateConnection();
        return await connection.ExecuteAsync(spName, parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string spName, DynamicParameters parameters)
    {
        using var connection = _context.CreateConnection();
        return await connection.QueryAsync<T>(spName, parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string spName, DynamicParameters parameters)
    {
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<T>(spName, parameters, commandType: CommandType.StoredProcedure);
    }
}
