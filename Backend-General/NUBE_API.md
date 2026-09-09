# ☁️ BaresFamilia.Nube.Api — Documentación Completa

API centralizada en la nube para el sistema BARES FAMILIA. Actúa como el nodo maestro de sincronización, reportes consolidados, gestión multi-sucursal y módulo de activación de dispositivos POS.

---

## 📋 Información General

| Propiedad | Valor |
|-----------|-------|
| **Proyecto** | `BaresFamilia.Nube.Api` |
| **Tipo** | ASP.NET Core Web API |
| **Puerto por defecto** | `5280` |
| **Base de datos** | PostgreSQL (`baresfamilia_nube`) |
| **DbContext** | `NubeContext` |
| **Autenticación** | JWT Bearer |
| **Swagger UI** | `http://localhost:5280/swagger` |

---

## 🎯 Responsabilidades

La API Nube es el **cerebro central** del sistema. Sus responsabilidades son:

1. **Maestro de datos de catálogo**: Mantiene la versión autoritativa de sucursales, productos, categorías, precios, roles y usuarios.
2. **Consolidación transaccional**: Recibe y almacena todas las transacciones sincronizadas desde las APIs locales (comandas, pagos, movimientos de caja).
3. **Reportes multi-sucursal**: Proporciona datos consolidados de todas las sucursales para dashboards gerenciales.
4. **Gestión de sincronización**: Resuelve conflictos cuando múltiples nodos locales envían datos simultáneamente.
5. **Administración centralizada**: ABM de sucursales, roles, usuarios, productos y configuración global.
6. **Módulo de Activación**: Genera códigos de activación para dispositivos POS y emite tokens JWT M2M de larga duración.

---

## 🔄 Auto-Migración de Base de Datos

La API aplica migraciones pendientes **automáticamente** al arrancar el servidor:

```csharp
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NubeContext>();
    context.Database.Migrate();
}
```

- **Al reiniciar**: Detecta y aplica cualquier migración nueva
- **Fallback seguro**: Si `Migrate()` falla, ejecuta `EnsureCreated()` como respaldo
- **Logging**: Registra el estado de la operación (éxito o error)

> No es necesario ejecutar `dotnet ef database update` manualmente en producción. Las tablas se crean/actualizan al iniciar la aplicación.

---

## 🗄️ Base de Datos — `baresfamilia_nube`

### Conexión

```
Host=localhost;Port=5432;Database=baresfamilia_nube;Username=postgres;Password=postgres
```

> ⚠️ Cambiar las credenciales para producción en `appsettings.Production.json`.

### Tablas del Dominio Catálogo

| Tabla | Entidad | Descripción |
|-------|---------|-------------|
| `Sucursales` | `Sucursal` | Locales/bares del negocio |
| `Roles` | `Rol` | Roles con permisos JSON |
| `Usuarios` | `Usuario` | Usuarios con auth dual (password + PIN) |
| `Categorias` | `Categoria` | Agrupación de productos (Bebidas, Comidas, etc.) |
| `Productos` | `Producto` | Ítems del menú con color UI y flag de cocina |
| `TiposVenta` | `TipoVenta` | Modalidades de venta (Salón, Delivery, Mostrador) |
| `ProductoPrecios` | `ProductoPrecio` | Precios segmentados por sucursal y tipo de venta |
| `MetodosPago` | `MetodoPago` | Métodos de pago con comisión y config AFIP |
| `Mesas` | `Mesa` | Mesas con posición en plano visual |

### Tablas del Dominio Transaccional

| Tabla | Entidad | SyncEstado | Descripción |
|-------|---------|------------|-------------|
| `Cajas` | `Caja` | ✅ | Cajas registradoras por sucursal |
| `TurnosCaja` | `TurnoCaja` | ✅ | Turnos de operación con arqueo |
| `Comandas` | `Comanda` | ✅ | Cabecera de pedidos/ventas |
| `ComandaItems` | `ComandaItem` | — | Líneas de detalle de cada comanda |
| `Pagos` | `Pago` | ✅ | Registros de cobro |
| `MovimientosCaja` | `MovimientoCaja` | ✅ | Ingresos/egresos fuera de ventas |

### Tablas del Dominio Seguridad

| Tabla | Entidad | Descripción |
|-------|---------|-------------|
| `DispositivosActivacion` | `DispositivoActivacion` | Registro de activación de POS con códigos y tokens M2M |

### Entidad Base (todas heredan)

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; }          // UUID — evita colisiones offline
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
| `FormaMesa` | `Cuadrada`, `Redonda`, `Rectangular`, `Irregular` | Forma visual de mesa |

---

## 🏗️ Arquitectura Interna

```
BaresFamilia.Nube.Api/
├── Controllers/
│   ├── ProductoController.cs         ← ABM completo de Producto
│   └── DispositivosController.cs     ← Módulo de Activación + JWT M2M
├── Program.cs                        ← Punto de entrada, JWT, auto-migrate
├── appsettings.json                  ← Connection string, JWT, logging
├── appsettings.Development.json      ← Override para desarrollo
└── Properties/
    └── launchSettings.json           ← Perfil de ejecución (puerto 5280)
```

### Flujo de una Request (ejemplo: Producto)

```
HTTP Request
    │
    ▼
[ProductoController]     ← Recibe request, valida DTOs, retorna response
    │
    ▼
[IProductoService]       ← Interfaz del servicio específico (Core.Models)
    │
    ▼
[ProductoService]        ← Hereda GenericService + métodos de dominio (Core.Models)
    │
    ▼
[IProductoRepository]    ← Interfaz del repositorio específico (Core.Models)
    │
    ▼
[ProductoRepository]     ← Hereda GenericRepository + queries con Include (Infrastructure)
    │
    ▼
[NubeContext]            ← DbContext PostgreSQL (Infrastructure)
    │
    ▼
PostgreSQL (baresfamilia_nube)
```

### Inyección de Dependencias

```csharp
// Registra NubeContext + repositorios/servicios genéricos (16 entidades)
builder.Services.AddNubeInfrastructure(builder.Configuration);

// Se registran adicionalmente las capas nominales de Producto:
// IProductoRepository → ProductoRepository
// IProductoService → ProductoService
```

---

## 🔐 Autenticación JWT

### Configuración

La API usa **JWT Bearer** para autenticación. Configurado en `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "BaresFamilia_S3cr3tK3y_2026_M1n1m0_32_Chars!!",
    "Issuer": "BaresFamilia.Nube.Api",
    "Audience": "BaresFamilia.Local.Api",
    "M2MTokenDurationDays": 365
  }
}
```

> ⚠️ **Producción**: Cambiar `SecretKey` por una clave segura de al menos 32 caracteres y almacenarla como variable de entorno o secret manager.

### Pipeline de Autenticación

```csharp
app.UseAuthentication();  // Valida el JWT
app.UseAuthorization();   // Verifica [Authorize]
app.MapControllers();
```

### Claims del Token M2M

| Claim | Descripción |
|-------|-------------|
| `sub` | ID del dispositivo |
| `tipo` | `"m2m"` — identifica como token de máquina |
| `sucursal_id` | UUID de la sucursal vinculada |
| `dispositivo_id` | UUID del dispositivo activado |
| `dispositivo_nombre` | Nombre descriptivo del POS |
| `iat` | Timestamp de emisión |
| `exp` | Expiración (365 días por defecto) |

---

## 📡 Endpoints Implementados

### 🛒 ProductoController — `/api/producto`

ABM completo con capas nominales: `ProductoController → IProductoService → ProductoService → IProductoRepository → ProductoRepository`

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| `GET` | `/api/producto` | 🔒 JWT | Listar todos los productos activos |
| `GET` | `/api/producto/{id}` | 🔒 JWT | Obtener producto con categoría y precios |
| `GET` | `/api/producto/por-categoria/{categoriaId}` | 🔒 JWT | Filtrar productos por categoría |
| `POST` | `/api/producto` | 🔒 JWT | Crear nuevo producto |
| `PUT` | `/api/producto/{id}` | 🔒 JWT | Actualizar producto existente |
| `DELETE` | `/api/producto/{id}` | 🔒 JWT | Borrado lógico (IsActive = false) |

#### DTOs de Request

**Crear Producto** — `POST /api/producto`
```json
{
  "categoriaId": "uuid",
  "nombre": "Cerveza Artesanal IPA",
  "colorUi": "#FF6B35",
  "requiereCocina": false
}
```

**Actualizar Producto** — `PUT /api/producto/{id}`
```json
{
  "categoriaId": "uuid",
  "nombre": "Cerveza Artesanal IPA",
  "colorUi": "#FF6B35",
  "requiereCocina": false
}
```

### 📱 DispositivosController — `/api/dispositivos`

Módulo de Activación de dispositivos POS. Gestiona el vínculo entre sucursales y terminales locales.

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| `POST` | `/api/dispositivos/generar-codigo` | 🔒 JWT | Genera código alfanumérico para una sucursal |
| `POST` | `/api/dispositivos/activar` | 🔓 Anónimo | Canjea código por JWT M2M de larga duración |

#### Flujo de Activación

```
┌─────────────────────────────────────────────────────────────┐
│ PASO 1: Admin genera código (requiere JWT)                  │
│                                                             │
│ POST /api/dispositivos/generar-codigo                       │
│ Body: { "sucursalId": "...", "nombreDispositivo": "POS 1" } │
│ Response: { "codigoActivacion": "BAR-7X9P-M2A1" }          │
│ (El código expira en 24 horas)                              │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼ (código se entrega al personal)
┌─────────────────────────────────────────────────────────────┐
│ PASO 2: POS Local activa el dispositivo (anónimo)           │
│                                                             │
│ POST /api/dispositivos/activar                              │
│ Body: { "codigoActivacion": "BAR-7X9P-M2A1" }              │
│ Response: {                                                 │
│   "token": "eyJhbG...",                                     │
│   "tokenType": "Bearer",                                   │
│   "expiresAt": "2027-05-07T...",                            │
│   "sucursalId": "...",                                      │
│   "sucursalNombre": "Bar Centro"                            │
│ }                                                           │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼ (POS guarda el JWT)
┌─────────────────────────────────────────────────────────────┐
│ PASO 3: POS usa el JWT para sincronizar                     │
│                                                             │
│ Headers: Authorization: Bearer eyJhbG...                    │
│ → Acceso autorizado a todos los endpoints protegidos        │
└─────────────────────────────────────────────────────────────┘
```

#### Seguridad del código de activación

- **Formato**: `BAR-XXXX-XXXX` (12 caracteres alfanuméricos)
- **Sin caracteres ambiguos**: Excluye 0/O, 1/I/L para evitar confusión
- **Generación criptográfica**: Usa `RandomNumberGenerator` (no `Random`)
- **Expiración**: 24 horas si no se utiliza
- **Uso único**: Una vez canjeado, no puede volver a usarse
- **Revocación**: Se almacena SHA-256 del token emitido

### 📡 Endpoints Futuros (por implementar)

#### Catálogo (CRUD — mismo patrón que Producto)

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET/POST/PUT/DELETE` | `/api/sucursal` | ABM de sucursales |
| `GET/POST/PUT/DELETE` | `/api/categoria` | ABM de categorías |
| `GET/POST/PUT/DELETE` | `/api/usuario` | ABM de usuarios |
| `GET/POST/PUT/DELETE` | `/api/metodo-pago` | ABM de métodos de pago |
| `GET/POST/PUT/DELETE` | `/api/mesa` | ABM de mesas |

#### Sincronización

| Método | Ruta | Descripción |
|--------|------|-------------|
| `POST` | `/api/sync/recibir` | Recibir datos desde API Local |
| `GET` | `/api/sync/catalogo` | Enviar catálogo actualizado a Local |
| `POST` | `/api/sync/resolver-conflicto` | Resolver conflictos de sincronización |

#### Reportes

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/reportes/ventas-diarias` | Ventas por día (multi-sucursal) |
| `GET` | `/api/reportes/arqueo/{turnoId}` | Detalle de arqueo de caja |
| `GET` | `/api/reportes/productos-top` | Productos más vendidos |

---

## 🚀 Ejecución

### Desarrollo (desde la raíz de la solución)

```bash
dotnet run --project src/BaresFamilia.Nube.Api
```

La API arranca en `http://localhost:5280`. Al iniciar:
1. Aplica migraciones pendientes automáticamente
2. Muestra Swagger UI en `/swagger`

### Con hot-reload (watch)

```bash
dotnet watch run --project src/BaresFamilia.Nube.Api
```

### Verificar que funciona

```bash
curl http://localhost:5280/swagger/index.html
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
    "NubeConnection": "Host=localhost;Port=5432;Database=baresfamilia_nube;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "SecretKey": "BaresFamilia_S3cr3tK3y_2026_M1n1m0_32_Chars!!",
    "Issuer": "BaresFamilia.Nube.Api",
    "Audience": "BaresFamilia.Local.Api",
    "M2MTokenDurationDays": 365
  }
}
```

### Variables de entorno (override)

```bash
# Override de connection string
export ConnectionStrings__NubeConnection="Host=mi-servidor.com;Port=5432;Database=baresfamilia_nube;Username=admin;Password=secreto"

# Override de JWT secret (recomendado en producción)
export Jwt__SecretKey="UnaClaveDeProduccionMuySeguraYLarga!!"

# Override de puerto
export ASPNETCORE_URLS="http://0.0.0.0:8080"
```

---

## 📦 Dependencias

| Paquete | Versión | Propósito |
|---------|---------|-----------|
| `Microsoft.AspNetCore.OpenApi` | 9.0.15 | Generación de spec OpenAPI |
| `Swashbuckle.AspNetCore` | 10.1.7 | Swagger UI |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0.15 | Autenticación JWT |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.15 | Herramientas de migración |

### Dependencias transitivas (via Infrastructure)

| Paquete | Versión | Propósito |
|---------|---------|-----------|
| `Microsoft.EntityFrameworkCore` | 9.0.15 | ORM |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 9.0.4 | Provider PostgreSQL |

---

## 🐘 Migraciones EF Core

> **Nota**: En producción no es necesario ejecutar migraciones manualmente. El `Program.cs` ejecuta `Database.Migrate()` automáticamente al arrancar.

### Comandos para desarrollo

```bash
# Crear nueva migración
dotnet ef migrations add NombreMigracion \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Nube.Api \
  --context NubeContext \
  --output-dir Migrations/Nube

# Aplicar migraciones manualmente
dotnet ef database update \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Nube.Api \
  --context NubeContext

# Revertir última migración
dotnet ef migrations remove \
  --project src/BaresFamilia.Infrastructure \
  --startup-project src/BaresFamilia.Nube.Api \
  --context NubeContext
```

> Requiere `dotnet-ef`. Instalar con: `dotnet tool install --global dotnet-ef`

### Migración actual

- `InitialCreate` — 17 tablas (9 catálogo + 6 transaccional + 1 seguridad + __EFMigrationsHistory)
