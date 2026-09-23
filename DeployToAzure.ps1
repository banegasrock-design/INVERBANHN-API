$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BaseDir = Resolve-Path "$ScriptDir\.."
$OutputDir = Join-Path $ScriptDir "azure_deploy_packages"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " INICIANDO PREPARACION DE DESPLIEGUE AZURE - INVERBANHN" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# 1. APIs .NET
$apis = @(
    @{ Name = "CustomerAPI"; Folder = "InverbanHN.CustomerAPI"; ZipName = "publish_customer_api.zip" },
    @{ Name = "VendorAPI";   Folder = "InverbanHN.VendorAPI";   ZipName = "publish_vendor_api.zip" },
    @{ Name = "AdminAPI";    Folder = "InverbanHN.AdminAPI";    ZipName = "publish_admin_api.zip" }
)

foreach ($api in $apis) {
    Write-Host "`nCompilando y empaquetando $($api.Name)..." -ForegroundColor Yellow
    $projectPath = Join-Path $ScriptDir $api.Folder
    $publishTemp = Join-Path $OutputDir "temp_$($api.Name)"
    $zipPath = Join-Path $OutputDir $api.ZipName

    if (Test-Path $publishTemp) { Remove-Item $publishTemp -Recurse -Force -ErrorAction SilentlyContinue }

    dotnet publish $projectPath -c Release -o $publishTemp --no-self-contained

    Start-Sleep -Seconds 2

    if (Test-Path $zipPath) { Remove-Item $zipPath -Force -ErrorAction SilentlyContinue }
    
    # Intentar compresión con reintento si hay archivo retenido temporalmente
    $maxAttempts = 3
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            Compress-Archive -Path "$publishTemp\*" -DestinationPath $zipPath -Force
            break
        } catch {
            if ($attempt -eq $maxAttempts) { throw $_ }
            Write-Host "  Reintentando empaquetado Zip ($attempt/$maxAttempts)..." -ForegroundColor DarkYellow
            Start-Sleep -Seconds 2
        }
    }

    Remove-Item $publishTemp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Paquete generado: $zipPath" -ForegroundColor Green
}

# 2. Frontends React / Vite
$frontends = @(
    @{ Name = "Usuario Final"; Folder = "INTERFAZ USUARIO-FINAL" },
    @{ Name = "Vendedor Tienda"; Folder = "INTERFAZ USUARIO-TIENDA" },
    @{ Name = "Administrador Global"; Folder = "INTERFAZ-ADMINISTRADOR" }
)

foreach ($fe in $frontends) {
    Write-Host "`nCompilando Frontend: $($fe.Name)..." -ForegroundColor Yellow
    $fePath = Join-Path $BaseDir $fe.Folder
    
    if (Test-Path $fePath) {
        Set-Location $fePath
        npm run build
        Write-Host "Compilacion finalizada en: $fePath\dist" -ForegroundColor Green
    } else {
        Write-Host "No se encontro el directorio: $fePath" -ForegroundColor Red
    }
}

Set-Location $ScriptDir

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host " PROCESO COMPLETADO EXITOSAMENTE" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "Paquetes Zip generados en: $OutputDir" -ForegroundColor Yellow
