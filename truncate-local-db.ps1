# Truncate Local Database Script for Bares Familia
Write-Host "Iniciando truncado de la base de datos local..." -ForegroundColor Cyan

$containerName = "bares_local_db"
$dbName = "baresfamilia_local"
$dbUser = "postgres"

# Comprobar si el contenedor está corriendo
$dockerRunning = docker ps --filter "name=$containerName" --format "{{.Names}}"

if ($dockerRunning -eq $containerName) {
    Write-Host "Contenedor '$containerName' encontrado. Ejecutando TRUNCATE dinámico..." -ForegroundColor Yellow
    
    # Query SQL dinámica para vaciar TODAS las tablas del esquema public (excepto el historial de migraciones)
    $sql = @'
DO $$ 
DECLARE 
    r RECORD; 
BEGIN 
    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory') LOOP 
        EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' RESTART IDENTITY CASCADE'; 
    END LOOP; 
END $$;
'@
    
    # Pasamos el comando SQL por standard input (stdin) para preservar el formato en PowerShell
    $sql | docker exec -i $containerName psql -U $dbUser -d $dbName
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ ¡Todas las tablas de la base de datos local fueron vaciadas con éxito!" -ForegroundColor Green
        Write-Host "Nota: Si deseas reiniciar el estado de activación del POS, elimina el archivo 'device_activation.json' generado en la carpeta de ejecución de Local.Api." -ForegroundColor Gray
    } else {
        Write-Host "❌ Error al ejecutar el comando en PostgreSQL." -ForegroundColor Red
    }
} else {
    Write-Host "⚠️ El contenedor '$containerName' no está en ejecución." -ForegroundColor Yellow
    Write-Host "Asegúrate de iniciar el ecosistema mediante 'docker-compose up --build' antes de ejecutar este script." -ForegroundColor Yellow
}
