using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data.Contexts;

public class DapperContext
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public DapperContext(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection") 
            ?? throw new System.ArgumentNullException("DefaultConnection no fue encontrada en appsettings.json");
    }

    public IDbConnection CreateConnection()
        => new SqlConnection(_connectionString);
}
