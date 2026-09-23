using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;

namespace Application.Interfaces;

public interface ISqlStoredProcedureRepository
{
    Task<int> ExecuteAsync(string spName, DynamicParameters parameters);
    Task<IEnumerable<T>> QueryAsync<T>(string spName, DynamicParameters parameters);
    Task<T?> QuerySingleOrDefaultAsync<T>(string spName, DynamicParameters parameters);
}
