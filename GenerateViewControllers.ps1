$modelsPath = "Models"
$outDir = "Controllers"

if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

# Filtrar solo archivos que comienzan con Vw y terminan en .cs
$viewFiles = Get-ChildItem -Path $modelsPath -Filter "Vw*.cs"

Write-Host "Se encontraron $($viewFiles.Count) vistas. Generando controladores de solo lectura..."

foreach ($file in $viewFiles) {
    $modelName = $file.BaseName
    $controllerName = "${modelName}Controller"
    $outFilePath = Join-Path $outDir "${controllerName}.cs"

    # Si el controlador ya existe, podemos saltarlo o sobrescribirlo. 
    # El usuario pidió "generar de todas", así que vamos a sobrescribir/crear.
    
    $template = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using INVERBANHN.Models;

namespace INVERBANHN.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ${modelName}Controller : ControllerBase
    {
        private readonly INVERBANHNContext _context;

        public ${modelName}Controller(INVERBANHNContext context)
        {
            _context = context;
        }

        // GET: api/${modelName}
        [HttpGet]
        public async Task<ActionResult<IEnumerable<${modelName}>>> Get${modelName}()
        {
            return await _context.Set<${modelName}>().ToListAsync();
        }
    }
}
"@

    Set-Content -Path $outFilePath -Value $template
    Write-Host "Generado: $controllerName"
}

Write-Host "Finalizado. Se generaron $($viewFiles.Count) controladores para las vistas."
