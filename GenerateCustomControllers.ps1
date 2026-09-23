$modelsPath = "Models"
$outDir = "Controllers"

if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$modelFiles = Get-ChildItem -Path $modelsPath -Filter "*.cs" | Where-Object { $_.Name -NotMatch "(Context|Vw|Detalle|Etiquetum)" }

foreach ($file in $modelFiles) {
    $modelName = $file.BaseName
    
    $content = Get-Content $file.FullName | Out-String
    
    $pkType = "string"
    $pkName = "Id"
    
    if ($content -match "public\s+(int|string|long|Guid|short|byte)\s+([A-Za-z0-9_]+Id|[A-Za-z0-9_]+ID|Id)\s*{\s*get;\s*set;\s*}") {
        $pkType = $matches[1]
        $pkName = $matches[2]
    } elseif ($content -match "public\s+(int|string|long|Guid|short|byte)\s+([A-Za-z0-9_]+)\s*{\s*get;\s*set;\s*}") {
        $pkType = $matches[1]
        $pkName = $matches[2]
    }

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
    public class $($modelName)Controller : ControllerBase
    {
        private readonly INVERBANHNContext _context;

        public $($modelName)Controller(INVERBANHNContext context)
        {
            _context = context;
        }

        // GET: api/$modelName
        [HttpGet]
        public async Task<ActionResult<IEnumerable<$modelName>>> Get$($modelName)s()
        {
            return await _context.Set<$modelName>().ToListAsync();
        }

        // GET: api/$modelName/5
        [HttpGet("{id}")]
        public async Task<ActionResult<$modelName>> Get$modelName($pkType id)
        {
            var entity = await _context.Set<$modelName>().FindAsync(id);

            if (entity == null)
            {
                return NotFound();
            }

            return entity;
        }

        // PUT: api/$modelName/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put$modelName($pkType id, $modelName entity)
        {
            if (id.ToString() != entity.$pkName.ToString())
            {
                return BadRequest();
            }

            _context.Entry(entity).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EntityExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/$modelName
        [HttpPost]
        public async Task<ActionResult<$modelName>> Post$modelName($modelName entity)
        {
            _context.Set<$modelName>().Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction("Get$modelName", new { id = entity.$pkName }, entity);
        }

        // DELETE: api/$modelName/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete$modelName($pkType id)
        {
            var entity = await _context.Set<$modelName>().FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            _context.Set<$modelName>().Remove(entity);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool EntityExists($pkType id)
        {
            return _context.Set<$modelName>().Any(e => e.$pkName.ToString() == id.ToString());
        }
    }
}
"@

    $outFilePath = Join-Path $outDir "${modelName}Controller.cs"
    Set-Content -Path $outFilePath -Value $template
    Write-Host "Generado: $outFilePath"
}

Write-Host "100% Finalizado. Controladores generados."
