using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace InverbanHN.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/apikeys")]
[Authorize(AuthenticationSchemes = "Bearer,ApiKey", Roles = "SuperAdmin")]
public class ApiKeyController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ApiKeyController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult GetActiveApiKeyConfig()
    {
        var headerName = _configuration["ApiKeySettings:HeaderName"] ?? "X-Api-Key";
        var appKey = _configuration["ApiKeySettings:AppKey"] ?? "IVB-ADMIN-ARMANDO-7722-BANEGAS-9911";

        return Ok(new
        {
            HeaderName = headerName,
            ActiveAppKey = appKey,
            Environment = "Production/Development",
            ManagedBy = "Jonathan / SuperAdmin Global"
        });
    }

    [HttpPost("generate")]
    public IActionResult GenerateNewApiKey([FromBody] GenerateKeyRequest request)
    {
        var newKey = $"IVB-{request.AppName.ToUpper()}-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
        return Ok(new
        {
            Message = "Nueva API Key generada con éxito.",
            AppName = request.AppName,
            ApiKey = newKey,
            CreatedAt = DateTime.UtcNow
        });
    }
}

public class GenerateKeyRequest
{
    public string AppName { get; set; } = "Integration";
}
