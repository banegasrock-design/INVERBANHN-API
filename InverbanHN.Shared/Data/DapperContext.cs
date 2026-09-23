using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace InverbanHN.Shared.Data;

public class DapperContext
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public DapperContext(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection") 
            ?? _configuration["ConnectionStrings:DefaultConnection"]
            ?? "Server=tcp:inverban.database.windows.net;Authentication=Active Directory Interactive;Database=INVERBANHN;TrustServerCertificate=True;MultipleActiveResultSets=true";
    }

    public IDbConnection CreateConnection()
    {
        try
        {
            var conn = new SqlConnection(_connectionString);
            conn.Open();
            DbInitializer.EnsureTablesExist(conn);
            return conn;
        }
        catch
        {
            var fallbackStr = "Server=(localdb)\\mssqllocaldb;Database=INVERBANHN;Trusted_Connection=True;MultipleActiveResultSets=true";
            var fallbackConn = new SqlConnection(fallbackStr);
            fallbackConn.Open();
            DbInitializer.EnsureTablesExist(fallbackConn);
            return fallbackConn;
        }
    }
}
