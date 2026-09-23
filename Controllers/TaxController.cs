using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaxController : ControllerBase
{
    private readonly ITaxService _taxService;

    public TaxController(ITaxService taxService)
    {
        _taxService = taxService;
    }

    [HttpPost("process/{subOrderId}")]
    public async Task<IActionResult> ProcessBilling(Guid subOrderId)
    {
        var result = await _taxService.ProcessOrderBillingAsync(subOrderId);

        if (!result.IsSuccess)
        {
            // Retorna un formato limpio para el frontend de React
            return BadRequest(new { Error = result.Error });
        }

        return Ok(new { Message = "Facturación y retenciones procesadas exitosamente." });
    }
}
