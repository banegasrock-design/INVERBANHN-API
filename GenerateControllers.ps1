Write-Host "Compilando proyecto base..."
dotnet build

$modelsPath = "Models"
$contextName = "INVERBANHNContext"
$outDir = "Controllers"

$modelFiles = Get-ChildItem -Path $modelsPath -Filter "*.cs" | Where-Object { $_.Name -ne "${contextName}.cs" -and $_.Name -notmatch "Vw" -and $_.Name -ne "Detalle.cs" -and $_.Name -ne "Etiquetum.cs" }

Write-Host "Se encontraron $($modelFiles.Count) modelos (excluyendo vistas). Iniciando..."

foreach ($file in $modelFiles) {
    $modelName = $file.BaseName
    $controllerName = "${modelName}Controller"
    
    Write-Host "Generando: $controllerName ..."
    dotnet aspnet-codegenerator controller -name $controllerName -async -api -m $modelName -dc $contextName -outDir $outDir --no-build
}

Write-Host "Completado. Verificando build final..."
dotnet build
