Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " PRUEBAS DE CONEXION DEL ECOSISTEMA DOCKER" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

function Test-Endpoint {
    param (
        [string]$Name,
        [string]$Url
    )
    Write-Host "Probando $Name en $Url ..." -NoNewline
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 304) {
            Write-Host " [OK] ($($response.StatusCode))" -ForegroundColor Green
        } else {
            Write-Host " [WARNING] ($($response.StatusCode))" -ForegroundColor Yellow
        }
    } catch {
        Write-Host " [ERROR] $($_.Exception.Message)" -ForegroundColor Red
    }
}

Test-Endpoint -Name "Nube API (Swagger)" -Url "http://localhost:5280/swagger/index.html"
Test-Endpoint -Name "Local API (Swagger)" -Url "http://localhost:5044/swagger/index.html"
Test-Endpoint -Name "Backoffice Frontend" -Url "http://localhost:5173"
Test-Endpoint -Name "Venta POS Frontend" -Url "http://localhost:5174"

Write-Host ""
Write-Host "------------------------------------------"
Write-Host "Revisando logs de Local API para verificar la conexión interna con Nube API..." -ForegroundColor Cyan
docker-compose logs --tail=20 local-api | Select-String -Pattern "SincronizacionWorker|Exception|Error|SocketException|HttpRequestException"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " FIN DE LAS PRUEBAS" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
