# 🏠 BaresFamilia.Local.Api — Documentación Completa

API local instalada en cada sucursal del sistema BARES FAMILIA. Opera de forma autónoma con su propia base de datos PostgreSQL, permitiendo funcionar sin conexión a internet y sincronizar cuando hay conectividad.

---

## 📋 Información General

| Propiedad | Valor |
|-----------|-------|
| **Proyecto** | `BaresFamilia.Local.Api` |
| **Tipo** | ASP.NET Core Web API |
| **Puerto por defecto** | `5044` |
| **Base de datos** | PostgreSQL (`baresfamilia_local`) |
| **DbContext** | `LocalContext` |
| **Autenticación hacia Nube** | JWT Bearer M2M (emitido por la API Nube) |
| **Swagger UI** | `http://localhost:5044/swagger` |

---

## 🎯 Responsabilidades

La API Local es el **motor operativo** de cada sucursal. Sus responsabilidades son:

1. **Punto de venta**: Gestión de comandas, cobros, impresión de tickets y flujo de cocina en tiempo real.
2. **Operación offline**: Funciona completamente sin internet usando su PostgreSQL local.
3. **Gestión de caja**: Apertura/cierre de turnos, arqueos, movimientos de ingreso/egreso.
4. **Plano de mesas**: Administración visual del salón con estados de mesas en tiempo real.
5. **Cola de sincronización**: Marca transacciones como `Pendiente` y las envía a la Nube cuando hay conectividad.
6. **Recepción de catálogo**: Descarga actualizaciones de productos, precios y configuración desde la Nube.
7. **Activación del dispositivo**: Al primer arranque, canjea un código de activación por un JWT M2M para autorizar la sincronización.

---

## 🗄️ Base de Datos — `baresfamilia_local`

### Conexión

```
Host=localhost;Port=5432;Database=baresfamilia_local;Username=postgres;Password=postgres
```

> ⚠️ En producción cada sucursal tendrá su propio PostgreSQL local con credenciales propias.

### Tablas del Dominio Catálogo

| Tabla | Entidad | Descripción |
|-------|---------|-------------|
| `Sucursales` | `Sucursal` | Datos de la sucursal actual (y referencia a otras) |
| `Roles` | `Rol` | Roles con permisos JSON |
| `Usuarios` | `Usuario` | Usuarios con auth dual (password + PIN) |
| `Categorias` | `Categoria` | Agrupación de productos |
| `Productos` | `Producto` | Ítems del menú con color UI y flag de cocina |
| `TiposVenta` | `TipoVenta` | Modalidades de venta |
| `ProductoPrecios` | `ProductoPrecio` | Precios segmentados |
| `MetodosPago` | `MetodoPago` | Métodos de pago con comisión y AFIP |
| `Mesas` | `Mesa` | Mesas con posición en plano visual |

### Tablas del Dominio Transaccional

| Tabla | Entidad | SyncEstado | Descripción |
|-------|---------|------------|-------------|
| `Cajas` | `Caja` | ✅ | Cajas registradoras de esta sucursal |
| `TurnosCaja` | `TurnoCaja` | ✅ | Turnos de operación con arqueo |
| `Comandas` | `Comanda` | ✅ | Cabecera de pedidos/ventas |
| `ComandaItems` | `ComandaItem` | — | Líneas de detalle |
| `Pagos` | `Pago` | ✅ | Registros de cobro |
| `MovimientosCaja` | `MovimientoCaja` | ✅ | Ingresos/egresos fuera de ventas |

### Tablas del Dominio Seguridad

| Tabla | Entidad | Descripción |
|-------|---------|-------------|
| `DispositivosActivacion` | `DispositivoActivacion` | Registro de activación del POS con código y token M2M |

### Entidad Base

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; }          // UUID — evita colisiones entre sucursales
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }     // Borrado lógico
}
```

### Enums del Sistema

| Enum | Valores | Uso |
|------|---------|-----|
| `SyncEstado` | `Pendiente`, `Sincronizado`, `Conflicto` | Estado de sincronización |
| `ComandaEstado` | `Abierta`, `Cobrada`, `Anulada` | Ciclo de vida de comanda |
| `EstadoPreparacion` | `Pendiente`, `EnPreparacion`, `Listo`, `Entregado` | Estado en cocina |
| `TipoCaja` | `Principal`, `Secundaria`, `Movil` | Clasificación de caja |
| `TipoMovimientoCaja` | `Ingreso`, `Egreso` | Movimiento de caja |
| `FormaMesa` | `Cuadrada`, `Redonda`, `Rectangular`, `Irregular` | Forma visual |

---

## 🏗️ Arquitectura Interna

```
BaresFamilia.Local.Api/
├── Controllers/                  ← Controladores de la API REST (ej. ComandaController)
├── Workers/                      ← BackgroundServices (SincronizacionWorker)
├── Program.cs                    ← Windows Service, Auto-Migrate, Pipeline HTTP
├── appsettings.json              ← Configuración (connection string, NubeApi)
├── appsettings.Development.json  ← Override para desarrollo
└── Properties/
    └── launchSettings.json       ← Perfil de ejecución (puerto 5044)
```

### Flujo de una Request

```
HTTP Request (desde POS/Frontend local)
    │
    ▼
[Controller]          ← Recibe la request, valida, retorna response
    │
    ▼
[IService<T>]         ← Interfaz del servicio (Core.Models)
    │
    ▼
[GenericService<T>]   ← Implementación con lógica de negocio (Core.Models)
    │
    ▼
[IRepository<T>]      ← Interfaz del repositorio (Core.Models)
    │
    ▼
[GenericRepository<T>] ← Implementación EF Core (Infrastructure)
    │
    ▼
[LocalContext]         ← DbContext PostgreSQL (Infrastructure)
    │
    ▼
PostgreSQL (baresfamilia_local)
```

### Inyección de Dependencias

```csharp
builder.Services.AddLocalInfrastructure(builder.Configuration);

// Registrar repositorios y servicios específicos
builder.Services.AddScoped<IComandaRepository, ComandaRepository>();
builder.Services.AddScoped<IComandaService, ComandaService>();

// HttpClient para la Nube
builder.Services.AddHttpClient("NubeApi", ...);

// Worker de Sincronización
builder.Services.AddHostedService<SincronizacionWorker>();
```

Registra automáticamente:
- `LocalContext` → PostgreSQL local
- `IRepository<T>` → `GenericRepository<T>` (16 entidades)
- `IService<T>` → `GenericService<T>` (16 entidades)
- Capas nominales específicas (ej. `IComandaService`)
- Tareas en segundo plano (`SincronizacionWorker`)

---

## 🚀 Ejecución

### Desarrollo (desde la raíz de la solución)

```bash
dotnet run --project src/BaresFamilia.Local.Api
```

La API arranca en `http://localhost:5044`.
> Al arrancar, aplica las **migraciones pendientes automáticamente** (`context.Database.Migrate()`) y ejecuta el fallback a `EnsureCreated` si falla.

### Instalación como Servicio de Windows (Producción)

El proyecto está configurado para ejecutarse como un servicio nativo de Windows (útil para el POS de la sucursal):

```csharp
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "BaresFamilia Local API";
});
```

Se instala publicando el proyecto y ejecutando `sc.exe create "BaresFamiliaLocal" binPath="C:\ruta\BaresFamilia.Local.Api.exe"`.

### Con hot-reload (watch)

```bash
dotnet watch run --project src/BaresFamilia.Local.Api
```

### Ejecutar ambas APIs simultáneamente

```bash
# Terminal 1 — Nube
dotnet run --project src/BaresFamilia.Nube.Api

# Terminal 2 — Local
dotnet run --project src/BaresFamilia.Local.Api
```

### Verificar que funciona

```bash
curl http://localhost:5044/swagger/index.html
```

---

## 🔧 Configuración

### `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "LocalConnection": "Host=localhost;Port=5432;Database=baresfamilia_local;Username=postgres;Password=postgres"
  },
  "NubeApi": {
    "BaseUrl": "http://localhost:5280",
    "JwtM2MToken": "TU_TOKEN_JWT_M2M_AQUI",
    "TimeoutSeconds": 30,
    "SyncIntervalSeconds": 30
  }
}
```

### Variables de entorno (override)

```bash
# Override de connection string
export ConnectionStrings__LocalConnection="Host=localhost;Port=5432;Database=baresfamilia_sucursal_centro;Username=admin;Password=clave_segura"

# Override de puerto
export ASPNETCORE_URLS="http://0.0.0.0:5044"
```

---

## 📦 Dependencias

| Paquete | Versión | Propósito |
|---------|---------|-----------|
| `Microsoft.AspNetCore.OpenApi` | 9.0.15 | Generación de spec OpenAPI |
| `Swashbuckle.AspNetCore` | 10.1.7 | Swagger UI |
| `Microsoft.Extensions.Hosting.WindowsServices` | 9.0.* | Soporte para Servicio Windows |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.* | Herramientas de migración |

### Dependencias transitivas (via Infrastructure)

| Paquete | Versión | Propósito |
|---------|---------|-----------|
| `Microsoft.EntityFrameworkCore` | 9.0.15 | ORM |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 9.0.4 | Provider PostgreSQL |

---

## 📡 Endpoints Implementados

### Operación del POS — `ComandaController`

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/comanda` | Listar todas |
| `GET` | `/api/comanda/{id}` | Obtener con detalles (ítems, pagos, mesa) |
| `GET` | `/api/comanda/mesa/{mesaId}` | Obtener comandas abiertas por mesa |
| `POST` | `/api/comanda` | Abrir nueva comanda |
| `POST` | `/api/comanda/{id}/cobrar` | Cobrar comanda (incluye integración AFIP) |
| `DELETE`| `/api/comanda/{id}` | Anular comanda |

#### Cobro con Integración Simulada a AFIP (WSFEv1)

El endpoint `POST /api/comanda/{id}/cobrar`:
1. Valida la comanda y registra el `Pago`.
2. Verifica si el `MetodoPago` tiene `RequiereFacturaAfip = true`.
3. Si es así, simula la obtención de **CAE** y Vencimiento.
4. Genera una **Orden de Impresión USB** en formato ESC/POS para la impresora fiscal.
5. Marca la comanda como `Cobrada`.

---

## ⚙️ Motor Local-First (SincronizacionWorker)

La API Local cuenta con un `BackgroundService` que se ejecuta cada 30 segundos:

1. **Recopilación**: Consulta en la base de datos `Comandas`, `Pagos` y `MovimientosCaja` que tengan `SyncEstado = Pendiente`.
2. **Empaquetado**: Convierte las entidades en un `SyncPayload` unificado y optimizado.
3. **Envío**: Ejecuta un `HTTP POST` hacia `api/sync/recibir` en la Nube.
4. **Seguridad**: Inyecta automáticamente el token JWT M2M en el header `Authorization: Bearer`.
5. **Confirmación**: Si la Nube devuelve HTTP 200 OK, actualiza los registros locales a `SyncEstado = Sincronizado`. Si hay error, reintenta en el próximo ciclo (garantizando funcionamiento offline ininterrumpido).

---

## 🐘 Migraciones EF Core

```bash
# Crear migración inicial
dotnet ef migrations add InitialCreate --project src/BaresFamilia.Infrastructure --startup-project src/BaresFamilia.Local.Api --context LocalContext

# Aplicar migraciones
dotnet ef database update --project src/BaresFamilia.Infrastructure --startup-project src/BaresFamilia.Local.Api --context LocalContext

# Revertir última migración
dotnet ef migrations remove --project src/BaresFamilia.Infrastructure --startup-project src/BaresFamilia.Local.Api --context LocalContext
```

> **Nota**: Requiere `dotnet-ef`. Instalar con: `dotnet tool install --global dotnet-ef`

---

## 🔑 Activación del Dispositivo POS

Antes de poder sincronizar con la API Nube, el POS local debe activarse:

```
1. Admin genera código en la Nube:
   POST https://api-nube.baresfamilia.com/api/dispositivos/generar-codigo
   Body: { "sucursalId": "...", "nombreDispositivo": "POS Caja 1" }
   → Response: { "codigoActivacion": "BAR-7X9P-M2A1" }

2. Personal ingresa el código en la pantalla de configuración del POS local.

3. POS Local canjea el código por un JWT M2M:
   POST https://api-nube.baresfamilia.com/api/dispositivos/activar
   Body: { "codigoActivacion": "BAR-7X9P-M2A1" }
   → Response: { "token": "eyJhbG...", "expiresAt": "2027-05-07" }

4. POS Local almacena el JWT y lo usa en cada request de sincronización:
   Authorization: Bearer eyJhbG...
```

> El JWT M2M tiene una duración de **365 días**. Al expirar, se debe generar un nuevo código de activación.

---

## 🔄 Ciclo de Vida Offline

```
1. Sucursal abre → API Local arranca → PostgreSQL local disponible
2. Mozo crea comanda → Se guarda con SyncEstado = "Pendiente"
3. Se cobra → Pago se guarda con SyncEstado = "Pendiente"
4. Hay internet → Servicio de sync envía todo a Nube (con JWT M2M)
5. Nube confirma → SyncEstado cambia a "Sincronizado"
6. Si hay conflicto → SyncEstado = "Conflicto" → Resolución manual
```
