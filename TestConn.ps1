Write-Host "Iniciando API de prueba en el puerto 5245..."
$process = Start-Process -FilePath "dotnet" -ArgumentList "run" -PassThru
Start-Sleep -Seconds 12

Write-Host "Probando conexión HTTP al controlador (y por ende, a la Base de Datos)..."
try {
    $r = Invoke-WebRequest -Uri "http://localhost:5245/api/Tblproducto" -UseBasicParsing
    Write-Host "STATUS CODE: $($r.StatusCode)"
    
    if ($r.Content -ne "[]") {
        $len = [math]::Min(300, $r.Content.Length)
        Write-Host "DATOS LEÍDOS DE LA BD (Preview): $($r.Content.Substring(0, $len))"
    } else {
        Write-Host "CONEXIÓN EXITOSA, PERO LA TABLA Tblproducto ESTÁ VACÍA (devuelve [])."
    }
} catch {
    Write-Host "ERROR EN LA PRUEBA:"
    Write-Host $_.Exception.Response
    Write-Host $_
}

try {
    Stop-Process -Id $process.Id -Force
} catch { }

Write-Host "API de prueba detenida."
