using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;

namespace INVERBANHN.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DispatchController : ControllerBase
{
    private readonly IDispatchService _dispatchService;

    public DispatchController(IDispatchService dispatchService)
    {
        _dispatchService = dispatchService;
    }

    /// <summary>
    /// Procesa el despacho de una orden pagada.
    /// Si la tienda es Ecommerce, la asigna a la bodega central de SPS.
    /// Si es Tienda, notifica al panel de la tienda vía SignalR.
    /// </summary>
    [HttpPost("process/{subOrderId}")]
    public async Task<IActionResult> ProcessDispatch(Guid subOrderId)
    {
        var result = await _dispatchService.ProcessDispatchAsync(subOrderId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { Error = result.Error });
        }

        return Ok(new { Message = result.Data });
    }
}
