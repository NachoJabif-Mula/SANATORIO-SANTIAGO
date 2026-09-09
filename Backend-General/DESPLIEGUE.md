# 🚀 BARES FAMILIA — Guía de Despliegue y Conexión

Guía completa para ejecutar, desplegar y conectar las APIs Nube y Local del sistema BARES FAMILIA.

---

## 📑 Índice

1. [Prerequisitos](#-prerequisitos)
2. [Ejecución Local (Desarrollo)](#-ejecución-local-desarrollo)
3. [Migraciones de Base de Datos](#-migraciones-de-base-de-datos)
4. [Despliegue de la API Nube en la nube](#-despliegue-de-la-api-nube-en-la-nube)
5. [Instalación de la API Local en la sucursal](#-instalación-de-la-api-local-en-la-sucursal)
6. [Conexión Nube ↔ Local](#-conexión-nube--local)
7. [Seguridad](#-seguridad)
8. [Monitoreo y Logs](#-monitoreo-y-logs)
9. [Troubleshooting](#-troubleshooting)

---

## 📦 Prerequisitos

### Para desarrollo

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- [dotnet-ef tool](https://learn.microsoft.com/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`
- Editor: Visual Studio 2022+ / VS Code / Rider

### Para producción (Nube)

- VPS / Cloud (AWS, Azure, DigitalOcean, Railway, Render, etc.)
- PostgreSQL managed o auto-hosted
- Dominio con SSL (Let's Encrypt / Cloudflare)

### Para producción (Local)

- PC o mini-PC con Windows/Linux en la sucursal
- PostgreSQL local instalado
- Acceso a internet (intermitente, no obligatorio)

---

## 💻 Ejecución Local (Desarrollo)

### Paso 1 — Crear las bases de datos

Conectarse a PostgreSQL y ejecutar:

```sql
CREATE DATABASE baresfamilia_nube;
CREATE DATABASE baresfamilia_local;
```

### Paso 2 — Instalar la herramienta EF Core

```bash
dotnet tool install --global dotnet-ef
```

### Paso 3 — Aplicar migraciones

> **Nota**: La API Nube aplica migraciones automáticamente al arrancar (`Database.Migrate()`), por lo que este paso es opcional. Sin embargo, es recomendable crear la migración inicial manualmente para tener el historial de cambios.

```bash
# Crear migración inicial para la API Nube (ya existe: InitialCreate)
dotnet ef migrations add InitialCreate \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Nube.Api \
  --context NubeContext \
  --output-dir Migrations/Nube

# Crear migración inicial para la API Local
dotnet ef migrations add InitialCreate \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Local.Api \
  --context LocalContext \
  --output-dir Migrations/Local
```

### Paso 4 — Ejecutar ambas APIs

Abrir **dos terminales** desde la raíz del proyecto:

```bash
# Terminal 1 — API Nube (puerto 5280)
dotnet run --project src/BaresFamilia.Nube.Api
```

```bash
# Terminal 2 — API Local (puerto 5044)
dotnet run --project src/BaresFamilia.Local.Api
```

### Paso 5 — Verificar

- Nube Swagger: http://localhost:5280/swagger
- Local Swagger: http://localhost:5044/swagger

> Al arrancar, la API Nube muestra en los logs:
> `Aplicando migraciones pendientes en la base de datos Nube...`
> `Migraciones aplicadas correctamente.`

### Modo watch (hot-reload para desarrollo)

```bash
# Terminal 1
dotnet watch run --project src/BaresFamilia.Nube.Api

# Terminal 2
dotnet watch run --project src/BaresFamilia.Local.Api
```

---

## 🐘 Migraciones de Base de Datos

### Comandos frecuentes

```bash
# ═══════════════════════════════════
# NUBE — Usar siempre --context NubeContext
# ═══════════════════════════════════

# Crear migración
dotnet ef migrations add NombreMigracion \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Nube.Api \
  --context NubeContext \
  --output-dir Migrations/Nube

# Aplicar migraciones pendientes
dotnet ef database update \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Nube.Api \
  --context NubeContext

# Revertir a migración específica
dotnet ef database update NombreMigracion \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Nube.Api \
  --context NubeContext

# Eliminar última migración (si no se aplicó)
dotnet ef migrations remove \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Nube.Api \
  --context NubeContext

# ═══════════════════════════════════
# LOCAL — Usar siempre --context LocalContext
# ═══════════════════════════════════

# Crear migración
dotnet ef migrations add NombreMigracion \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Local.Api \
  --context LocalContext \
  --output-dir Migrations/Local

# Aplicar migraciones pendientes
dotnet ef database update \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Local.Api \
  --context LocalContext
```

---

## ☁️ Despliegue de la API Nube en la nube

### Opción A — VPS con Docker (recomendado)

#### 1. Crear `Dockerfile` para la API Nube

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "BaresFamilia.sln"
RUN dotnet publish "src/BaresFamilia.Nube.Api/BaresFamilia.Nube.Api.csproj" \
    -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BaresFamilia.Nube.Api.dll"]
```

#### 2. `docker-compose.yml`

```yaml
version: '3.8'
services:
  nube-api:
    build:
      context: .
      dockerfile: Dockerfile.Nube
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__NubeConnection=Host=db;Port=5432;Database=baresfamilia_nube;Username=admin;Password=clave_produccion_segura
    depends_on:
      - db

  db:
    image: postgres:16-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_USER: admin
      POSTGRES_PASSWORD: clave_produccion_segura
      POSTGRES_DB: baresfamilia_nube
    volumes:
      - pgdata_nube:/var/lib/postgresql/data

volumes:
  pgdata_nube:
```

#### 3. Desplegar

```bash
# En el VPS
docker compose up -d

# Verificar
curl http://tu-servidor:8080/swagger/index.html
```

### Opción B — Publicar directamente en un VPS Linux

```bash
# Compilar para producción
dotnet publish src/BaresFamilia.Nube.Api -c Release -o ./publish/nube

# Copiar al servidor
scp -r ./publish/nube user@mi-servidor.com:/opt/baresfamilia/nube

# En el servidor — ejecutar
cd /opt/baresfamilia/nube
export ConnectionStrings__NubeConnection="Host=localhost;Port=5432;Database=baresfamilia_nube;Username=admin;Password=clave_segura"
export Jwt__SecretKey="ClaveDeProduccionMuySegura32CharsMinimo!!"
export ASPNETCORE_URLS="http://0.0.0.0:8080"
dotnet BaresFamilia.Nube.Api.dll
```

### Opción C — Servicio systemd (Linux)

Crear `/etc/systemd/system/baresfamilia-nube.service`:

```ini
[Unit]
Description=BaresFamilia Nube API
After=network.target postgresql.service

[Service]
WorkingDirectory=/opt/baresfamilia/nube
ExecStart=/usr/bin/dotnet /opt/baresfamilia/nube/BaresFamilia.Nube.Api.dll
Restart=always
RestartSec=10
User=baresfamilia
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:8080
Environment=ConnectionStrings__NubeConnection=Host=localhost;Port=5432;Database=baresfamilia_nube;Username=admin;Password=clave_segura
Environment=Jwt__SecretKey=ClaveDeProduccionMuySegura32CharsMinimo!!

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable baresfamilia-nube
sudo systemctl start baresfamilia-nube
sudo systemctl status baresfamilia-nube
```

### Configurar Nginx como Reverse Proxy + SSL

```nginx
server {
    listen 443 ssl;
    server_name api-nube.baresfamilia.com;

    ssl_certificate     /etc/letsencrypt/live/api-nube.baresfamilia.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api-nube.baresfamilia.com/privkey.pem;

    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 🏠 Instalación de la API Local en la sucursal

### Opción A — Servicio Windows (recomendado para Windows)

#### 1. Publicar

```bash
dotnet publish src/BaresFamilia.Local.Api -c Release -o ./publish/local --self-contained -r win-x64
```

#### 2. Instalar como servicio Windows

```powershell
# Copiar a carpeta de instalación
Copy-Item -Recurse ./publish/local C:/BaresFamilia/Local

# Crear servicio Windows
sc.exe create "BaresFamiliaLocal" `
  binPath="C:\BaresFamilia\Local\BaresFamilia.Local.Api.exe" `
  start=auto `
  DisplayName="BaresFamilia - API Local"

# Configurar variables de entorno del servicio
# Editar C:\BaresFamilia\Local\appsettings.Production.json con:
```

```json
{
  "ConnectionStrings": {
    "LocalConnection": "Host=localhost;Port=5432;Database=baresfamilia_local;Username=postgres;Password=clave_local"
  },
  "NubeApi": {
    "BaseUrl": "https://api-nube.baresfamilia.com",
    "TimeoutSeconds": 30,
    "RetryCount": 3,
    "SyncIntervalMinutes": 5
  }
}
```

> **Nota**: El JWT M2M se obtiene durante la activación del dispositivo y se almacena localmente. No necesita configurarse manualmente en el appsettings.

```powershell
# Iniciar servicio
sc.exe start BaresFamiliaLocal

# Verificar
Invoke-WebRequest http://localhost:5044/swagger/index.html
```

#### 3. Configurar inicio automático

El servicio se configura con `start=auto`, arranca solo cuando encienden la PC.

### Opción B — Servicio systemd (Linux)

```bash
# Publicar
dotnet publish src/BaresFamilia.Local.Api -c Release -o ./publish/local

# Copiar al equipo de la sucursal
scp -r ./publish/local user@sucursal-pc:/opt/baresfamilia/local
```

Crear `/etc/systemd/system/baresfamilia-local.service`:

```ini
[Unit]
Description=BaresFamilia Local API
After=network.target postgresql.service

[Service]
WorkingDirectory=/opt/baresfamilia/local
ExecStart=/usr/bin/dotnet /opt/baresfamilia/local/BaresFamilia.Local.Api.dll
Restart=always
RestartSec=10
User=baresfamilia
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5044
Environment=ConnectionStrings__LocalConnection=Host=localhost;Port=5432;Database=baresfamilia_local;Username=admin;Password=clave_local

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable baresfamilia-local
sudo systemctl start baresfamilia-local
```

---

## 🔗 Conexión Nube ↔ Local

### Diagrama de Comunicación

```
┌──────────────────────────────────────┐
│         INTERNET / CLOUD             │
│                                      │
│  ┌────────────────────────────────┐  │
│  │  api-nube.baresfamilia.com     │  │
│  │  (VPS + Nginx + SSL)          │  │
│  │  Puerto externo: 443 (HTTPS)  │  │
│  │  Puerto interno: 8080         │  │
│  │  DB: baresfamilia_nube        │  │
│  └──────────────┬─────────────────┘  │
│                 │                    │
└─────────────────┼────────────────────┘
                  │  HTTPS
                  │
    ┌─────────────┼──────────────────────────────────┐
    │   SUCURSAL   │    (Red local / WiFi)            │
    │             │                                  │
    │  ┌──────────▼───────────────────┐              │
    │  │  API Local                   │              │
    │  │  http://localhost:5044       │              │
    │  │  DB: baresfamilia_local      │◄─── POS/App  │
    │  └──────────────────────────────┘              │
    │                                                │
    └────────────────────────────────────────────────┘
```

### Configuración de la API Local para conectar con la Nube

Agregar en `appsettings.Production.json` de la API Local:

```json
{
  "ConnectionStrings": {
    "LocalConnection": "Host=localhost;Port=5432;Database=baresfamilia_local;Username=postgres;Password=clave_local"
  },
  "NubeApi": {
    "BaseUrl": "https://api-nube.baresfamilia.com",
    "TimeoutSeconds": 30,
    "RetryCount": 3,
    "SyncIntervalMinutes": 5
  }
}
```

### Activación del Dispositivo POS (primer arranque)

Antes de sincronizar, el POS local debe activarse una única vez:

```
1. Admin autenticado genera código en la Nube:
   POST https://api-nube.baresfamilia.com/api/dispositivos/generar-codigo
   Body: { "sucursalId": "...", "nombreDispositivo": "POS Caja 1" }
   → Response: { "codigoActivacion": "BAR-7X9P-M2A1" } (expira en 24h)

2. Personal ingresa el código en la configuración del POS local.

3. POS Local canjea el código por JWT M2M (365 días):
   POST https://api-nube.baresfamilia.com/api/dispositivos/activar
   Body: { "codigoActivacion": "BAR-7X9P-M2A1" }
   → Response: { "token": "eyJhbG...", "expiresAt": "2027-05-07" }

4. POS guarda el JWT y lo usa en cada sync:
   Authorization: Bearer eyJhbG...
```

### Flujo de Sincronización (por implementar)

#### 1. Local → Nube (Push de transacciones)

```
API Local cada 5 minutos:
  1. Consultar registros con SyncEstado = "Pendiente"
  2. Armar batch JSON con las transacciones
  3. POST https://api-nube.baresfamilia.com/api/sync/recibir
     Headers: { "Authorization": "Bearer eyJhbG..." }  ← JWT M2M
     Body: { "sucursalId": "...", "comandas": [...], "pagos": [...], "movimientos": [...] }
  4. Si 200 OK → Marcar SyncEstado = "Sincronizado"
  5. Si error → Reintentar en el próximo ciclo
  6. Si conflicto 409 → Marcar SyncEstado = "Conflicto"
```

#### 2. Nube → Local (Pull de catálogo)

```
API Local al iniciar y cada 30 minutos:
  1. GET https://api-nube.baresfamilia.com/api/sync/catalogo?desde={ultimaSync}
     Headers: { "Authorization": "Bearer eyJhbG..." }  ← JWT M2M
  2. Recibe productos, precios, categorías actualizados
  3. Upsert en la DB local
  4. Guarda timestamp de última sincronización
```

#### 3. Resolución de conflictos

```
Cuando SyncEstado = "Conflicto":
  1. Nube notifica al dashboard gerencial
  2. Admin revisa los datos en conflicto
  3. POST /api/sync/resolver-conflicto con la versión correcta
  4. Nube actualiza y notifica a la sucursal
  5. Local marca como "Sincronizado"
```

### Implementación del HttpClient (a agregar en API Local)

```csharp
// Futuro: Registrar en Program.cs de la API Local
builder.Services.AddHttpClient("NubeApi", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["NubeApi:BaseUrl"]!);
    // El JWT M2M se almacena localmente tras la activación
    // y se agrega dinámicamente en cada request del SyncService
    client.Timeout = TimeSpan.FromSeconds(
        config.GetValue<int>("NubeApi:TimeoutSeconds", 30));
});
```

---

## 🔒 Seguridad

### Checklist de Producción

- [ ] Cambiar todas las contraseñas default de PostgreSQL
- [ ] Usar HTTPS en la API Nube (SSL/TLS obligatorio)
- [x] ~~Implementar autenticación JWT~~ ✔️ Implementado (JWT Bearer + M2M)
- [x] ~~Módulo de Activación de Dispositivos~~ ✔️ Implementado
- [ ] Configurar CORS restrictivo en la API Nube
- [ ] Firewall: solo permitir puerto 443 en el VPS
- [ ] Firewall: API Local solo accesible desde la red local
- [ ] Encriptar los `PasswordHash` con bcrypt/Argon2
- [ ] Cambiar `Jwt:SecretKey` por variable de entorno en producción
- [ ] Rotar JWT M2M periódicamente (regenerar códigos de activación)
- [ ] Backups automáticos de PostgreSQL

### CORS para la API Nube (a agregar)

```csharp
// En Program.cs de la API Nube
builder.Services.AddCors(options =>
{
    options.AddPolicy("SucursalesPolicy", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5044",           // Dev local
            "https://sucursal-centro.local",   // Sucursales conocidas
            "https://sucursal-norte.local"
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});
```

---

## 📊 Monitoreo y Logs

### Logs por defecto

Ambas APIs loguean a la consola. La configuración está en `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

### Niveles recomendados por entorno

| Categoría | Development | Production |
|-----------|-------------|------------|
| Default | Information | Warning |
| Microsoft.AspNetCore | Warning | Warning |
| Microsoft.EntityFrameworkCore | Information | Warning |
| BaresFamilia | Debug | Information |

### Ver logs del servicio (Linux)

```bash
# API Nube
journalctl -u baresfamilia-nube -f

# API Local
journalctl -u baresfamilia-local -f
```

### Ver logs del servicio (Windows)

```powershell
# Ver Event Viewer
Get-EventLog -LogName Application -Source "BaresFamiliaLocal" -Newest 50
```

---

## 🔧 Troubleshooting

### La API no arranca

```bash
# Verificar que PostgreSQL está corriendo
sudo systemctl status postgresql        # Linux
Get-Service postgresql*                  # Windows

# Verificar conexión a la BD
psql -h localhost -U postgres -d baresfamilia_nube -c "SELECT 1;"

# Verificar que el puerto no está ocupado
netstat -tlnp | grep 5280               # Linux
netstat -an | findstr "5280"             # Windows
```

### Error de migración

```bash
# Verificar que la BD existe
psql -h localhost -U postgres -c "\l" | grep baresfamilia

# Forzar re-creación de la BD
psql -h localhost -U postgres -c "DROP DATABASE IF EXISTS baresfamilia_nube;"
psql -h localhost -U postgres -c "CREATE DATABASE baresfamilia_nube;"
dotnet ef database update --project src/BaresFamilia.Infrastructure --startup-project src/BaresFamilia.Nube.Api --context NubeContext
```

### La sincronización falla

```
1. Verificar conectividad: curl https://api-nube.baresfamilia.com/swagger/index.html
2. Verificar que el JWT M2M no haya expirado
3. Si expiró: generar nuevo código de activación y reactivar
4. Revisar logs de la API Local: journalctl -u baresfamilia-local -f
5. Verificar registros con SyncEstado = "Conflicto" en la BD local
6. Verificar espacio en disco del PostgreSQL local
```

### Puertos en uso

```bash
# Cambiar puerto de la API Nube
export ASPNETCORE_URLS="http://0.0.0.0:9090"

# O editar launchSettings.json
# "applicationUrl": "http://localhost:9090"
```

---

## 📋 Resumen de Puertos y URLs

| Servicio | Entorno | URL | Puerto |
|----------|---------|-----|--------|
| API Nube | Desarrollo | http://localhost:5280 | 5280 |
| API Nube | Producción | https://api-nube.baresfamilia.com | 443 → 8080 |
| API Local | Desarrollo | http://localhost:5044 | 5044 |
| API Local | Producción | http://localhost:5044 | 5044 |
| PostgreSQL Nube | Desarrollo | localhost | 5432 |
| PostgreSQL Nube | Producción | managed/VPS | 5432 |
| PostgreSQL Local | Producción | localhost (sucursal) | 5432 |
| Swagger Nube | Desarrollo | http://localhost:5280/swagger | — |
| Swagger Local | Desarrollo | http://localhost:5044/swagger | — |
