using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportsService _reportsService;

    public ReportsController(IReportsService reportsService)
    {
        _reportsService = reportsService;
    }

    [HttpGet("bank-exports")]
    public async Task<IActionResult> GetBankExports()
    {
        var result = await _reportsService.GetBankExportsAsync();

        if (!result.IsSuccess)
        {
            return BadRequest(new { Error = result.Error });
        }

        // Retorna un arreglo limpio directamente para TanStack Table
        return Ok(result.Data);
    }
}
