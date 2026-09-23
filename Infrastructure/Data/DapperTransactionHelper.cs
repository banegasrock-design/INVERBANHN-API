using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Data;

public class DapperTransactionHelper
{
    private readonly string _connectionString;

    public DapperTransactionHelper(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException("Connection string is missing");
    }

    /// <summary>
    /// Ejecuta una acción dentro de una transacción, estableciendo previamente el SESSION_CONTEXT
    /// para que los triggers de auditoría (ej: GS_Auditoria) registren al usuario correcto.
    /// </summary>
    public async Task<T> ExecuteWithAuditAsync<T>(string adminId, Func<IDbConnection, IDbTransaction, Task<T>> action)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Establecer el contexto de sesión para la auditoría
            await connection.ExecuteAsync(
                "EXEC sp_set_session_context @key = N'AdminID', @value = @AdminId;",
                new { AdminId = adminId },
                transaction: transaction
            );

            // 2. Ejecutar la acción principal (INSERT, UPDATE, DELETE)
            var result = await action(connection, transaction);

            // 3. Confirmar la transacción
            transaction.Commit();

            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    
    /// <summary>
    /// Ejecuta una acción dentro de una transacción sin retorno, estableciendo previamente el SESSION_CONTEXT.
    /// </summary>
    public async Task ExecuteWithAuditAsync(string adminId, Func<IDbConnection, IDbTransaction, Task> action)
    {
        await ExecuteWithAuditAsync<int>(adminId, async (conn, tx) =>
        {
            await action(conn, tx);
            return 1;
        });
    }
}
