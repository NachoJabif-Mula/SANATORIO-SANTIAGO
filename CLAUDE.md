# 🍺 BARES FAMILIA — Contexto de Desarrollo y Reglas de Programación

Este archivo proporciona el contexto base, los comandos y las reglas de programación para asistir en el desarrollo de la aplicación **BARES FAMILIA**.

---

## 📋 Resumen del Proyecto

**BARES FAMILIA** es un sistema integral de gestión para cadenas de bares con una **arquitectura local-first dual (Nube/Local)**. Permite operar de forma autónoma en cada sucursal (incluso sin conexión a internet) y sincronizar los datos transaccionales con un servidor central en la nube cuando hay conectividad.

El ecosistema está compuesto por:
1. **API Nube (Maestro de Catálogo y Consolidador)**: Actúa como base central, gestiona ABM globales (sucursales, productos, precios, usuarios) y emite tokens M2M para sincronización de sucursales.
2. **API Local (Motor Operativo de Sucursal)**: Gestiona comandas, cobros, arqueos, y flujo de cocina localmente. Cuenta con un background service (`SincronizacionWorker`) que envía los datos pendientes a la Nube.
3. **Backoffice Frontend (Panel de Control)**: Aplicación React en la nube para administración central y visualización de reportes consolidados.
4. **Venta POS Frontend (Punto de Venta)**: Aplicación React local optimizada para uso táctil en modo quiosco, con modo oscuro de alto contraste y locks de seguridad mediante PIN de gerente.

---

## 📁 Estructura del Workspace

```
SANATORIO-SANTIAGO/
├── Backend-General/               → Backend en C# (.NET 9)
│   ├── BaresFamilia.sln           → Solución de Visual Studio
│   ├── src/
│   │   ├── BaresFamilia.Nube.Api/       → API Centralizada (puerto: 5280)
│   │   ├── BaresFamilia.Local.Api/      → API Local de Sucursal (puerto: 5044)
│   │   ├── BaresFamilia.Core.Models/    → Dominio: entidades, contratos y lógica de negocio
│   │   └── BaresFamilia.Infrastructure/ → Acceso a Datos (EF Core, Repositorios, DI)
│   └── tests/
│       └── BaresFamilia.Tests/          → Pruebas de arquitectura e integración
├── Backoffice-Front/              → Panel web de administración (React, puerto: 5173)
├── Venta-Front/                   → Terminal de venta táctil (React, puerto: 5174)
└── docker-compose.yml             → DBs Postgres locales de desarrollo
```

### Interior de `BaresFamilia.Core.Models`

```
BaresFamilia.Core.Models/
├── Entities/      → Entidades de dominio (mapeadas por EF, heredan de BaseEntity)
├── Enums/
├── Interfaces/    → SOLO contratos de comportamiento (IXService, IXRepository)
├── Services/      → Implementación de la lógica de negocio
├── Exceptions/    → Excepciones de dominio que la API traduce a códigos HTTP
├── Dtos/          → Espejos de tablas de la base (ej. SyncProductoDto ↔ Producto)
│   └── <Área>/
└── Contratos/     → Lo que entra y sale por la API
    └── <Área>/        Solicitudes*.cs (requests) · Resultados*.cs (responses)
```

---

## 🔒 REGLAS DE ORO DE ARQUITECTURA (NO NEGOCIABLES)

> Estas reglas **no son sugerencias**. El backend fue refactorizado completo para cumplirlas y hay
> pruebas automáticas que fallan si se rompen (`tests/BaresFamilia.Tests/ArquitecturaPorCapasTests.cs`).
> Si una tarea parece exigir violarlas, **no las violes**: avisá y proponé cómo resolverla dentro de la arquitectura.

### 1. El flujo de capas es obligatorio y no se saltea

```
Controller → IServicio → Servicio → IRepositorio → Repositorio → DbContext
```

Ninguna capa puede saltar a la de más abajo. Un Controller nunca habla con un repositorio ni con el
contexto; un Servicio nunca habla con el contexto.

### 2. Controller: solo HTTP

**Hace**: leer el request, mapearlo, resolver identidad/alcance desde los claims, elegir el código de
respuesta y proyectar el resultado.

**Tiene prohibido**:
- Inyectar `NubeContext`, `LocalContext` o `DbContext`.
- Usar EF Core (`ToListAsync`, `Include`, `FirstOrDefaultAsync`, `AnyAsync`, `SaveChangesAsync`…).
- Contener lógica de negocio, cálculos, validaciones de dominio o reglas de integridad.
- Depender de clases concretas: **siempre** se inyecta la interfaz (`IComandaService`, no `ComandaService`).

### 3. Servicio: toda la lógica de negocio

**Hace**: validaciones, reglas, cálculos, orquestación entre repositorios.

**Tiene prohibido**:
- Usar EF Core o cualquier `DbContext`.
- Devolver tipos HTTP (`IActionResult`, `NotFound()`, `BadRequest()`). Para fallar, **lanza una excepción de dominio** (ver regla 5).

### 4. Repositorio: único lugar con EF Core

- Es la **única** capa que puede usar Entity Framework.
- El constructor recibe `DbContext` (la abstracción), **nunca** `NubeContext` ni `LocalContext`: así la
  misma clase se registra en ambas APIs. Se resuelve en DI con
  `sp => new XRepository(sp.GetRequiredService<NubeContext>())`.
- Hereda de `GenericRepository<T>` e implementa su interfaz declarada en `Core.Models/Interfaces`.
- **Consultas por lote, nunca N+1**: si hay que verificar N ids, se hace una consulta con `Contains`,
  no N consultas dentro de un `foreach`.

### 5. Los errores viajan como excepciones de dominio

`Core.Models/Exceptions` + `ManejadorExcepcionesMiddleware` (uno por API):

| Excepción                      | HTTP |
|--------------------------------|------|
| `ReglaNegocioException`        | 400  |
| `RecursoNoEncontradoException` | 404  |
| `AccesoDenegadoException`      | 403  |
| `IntegracionNubeException`     | 500  |

`ReglaNegocioException` acepta además un `Codigo` y `Detalles` para los errores que el POS interpreta
de forma especial (ej. `MesasAbiertas` + `count`, que dispara el diálogo de transferencia de mesas).
**No inventar nuevos códigos** sin verificar qué espera el frontend.

### 6. Contratos de datos: `Dtos/` vs `Contratos/`

- **`Dtos/`** → espejos de tablas de la base (`SyncProductoDto`, `CierreDiarioDto`, `UsuarioDto`).
- **`Contratos/`** → lo que entra y sale por la API, separando `Solicitudes*.cs` de `Resultados*.cs`.

**Tienen prohibido** declararse dentro de un Controller, un Worker, un Hub o un archivo de interfaz.
`Interfaces/` lleva **solo** contratos de comportamiento.

### 7. `Core.Models` no conoce la infraestructura

No puede referenciar `Microsoft.EntityFrameworkCore` ni `BaresFamilia.Infrastructure`. Si algo del
dominio “necesita” EF, es señal de que esa lógica va en un repositorio.

### 8. Workers, Hubs y `Program.cs` cumplen lo mismo

- `SincronizacionWorker` y `PrintHub` acceden a datos **solo** por repositorios/servicios.
- Nada de estado estático global (`static class ...Store`): si hace falta estado compartido, va como
  **singleton inyectado** detrás de una interfaz (ver `IMonitorSincronizacion`).
- `Program.cs` es solo cableado: migraciones y seed viven en `Infrastructure/Data/Inicializador*.cs`.

### 9. Todo lo nuevo se registra en un solo lugar

`Infrastructure/DependencyInjection.cs`. Los repositorios compartidos por ambas APIs se suman a los
helpers por contexto (`RegistrarCatalogo<TContext>`, `RegistrarTransaccional<TContext>`).

### 10. Antes de dar una tarea por terminada

```powershell
dotnet build Backend-General/BaresFamilia.sln     # 0 errores y 0 warnings
dotnet test Backend-General/tests/BaresFamilia.Tests/BaresFamilia.Tests.csproj
```

Ambos tienen que pasar. **No se entrega con warnings nuevos.**

---

### ✅ Checklist para agregar un endpoint

1. ¿Necesita una consulta nueva? → método en `IXRepository` + implementación en `XRepository`.
2. ¿Tiene reglas o validaciones? → método en `IXService` + implementación en `XService`, lanzando excepciones de dominio.
3. ¿Recibe o devuelve datos? → `record` en `Contratos/<Área>/Solicitudes*.cs` o `Resultados*.cs`.
4. Registrar lo nuevo en `DependencyInjection.cs`.
5. Controller: mapear, autorizar, delegar y devolver. Nada más.
6. Test de integración del servicio en `tests/BaresFamilia.Tests/Integracion/`.
7. `dotnet build` + `dotnet test` en verde.

### ❌ Ejemplos de lo que se rechaza

```csharp
// MAL: el controller conoce la base y arma la query
public class ProductoController : ControllerBase
{
    public ProductoController(NubeContext context) { ... }          // ← prohibido

    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _context.Productos.Where(p => p.IsActive).ToListAsync(ct));  // ← prohibido
}

// MAL: el servicio devuelve HTTP y usa EF
public async Task<IActionResult> CrearAsync(Producto p)              // ← prohibido
{
    if (await _context.Productos.AnyAsync(...)) return BadRequest(); // ← prohibido
}

// MAL: DTO declarado dentro del controller
public record CreateProductoRequest(string Nombre);                  // ← va en Contratos/Catalogos/
```

```csharp
// BIEN
public class ProductoController : ControllerBase
{
    private readonly IProductoService _productoService;              // interfaz

    public async Task<IActionResult> GetAll([FromQuery] Guid? sucursalId, CancellationToken ct)
    {
        var sucId = User.IsGlobal() ? sucursalId : User.GetSucursalId();   // identidad: sí es del controller
        return Ok(await _productoService.GetPorSucursalAsync(sucId, false, ct));
    }
}

// El servicio valida y lanza excepción de dominio; el middleware la vuelve 400.
public async Task<Producto> CrearAsync(Producto producto, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(producto.Nombre))
        throw new ReglaNegocioException("El nombre del producto es obligatorio.");

    return await CreateAsync(producto, ct);
}
```

---

## ⚙️ Comandos Útiles

### Bases de Datos (PostgreSQL via Docker)
Asegurar el inicio de los contenedores de base de datos antes de arrancar las APIs:
```powershell
docker-compose up -d
# nube-db corre en puerto 5433 (baresfamilia_nube)
# local-db corre en puerto 5434 (baresfamilia_local)
```

### Ejecutar Backend (.NET 9)
Desde la carpeta `Backend-General`:
```powershell
# Ejecutar API Nube (Puerto 5280)
dotnet run --project src/BaresFamilia.Nube.Api

# Ejecutar API Local (Puerto 5044)
dotnet run --project src/BaresFamilia.Local.Api
```

### Compilar y Probar el Backend
Desde la carpeta `Backend-General`. **Ambos deben pasar antes de dar una tarea por terminada:**
```powershell
# Debe terminar con 0 errores y 0 warnings
dotnet build BaresFamilia.sln

# Pruebas de arquitectura + integración (usan SQLite en memoria, no requieren Docker)
dotnet test tests/BaresFamilia.Tests/BaresFamilia.Tests.csproj
```

> Si fallan las pruebas de `ArquitecturaPorCapasTests`, el problema **no es el test**: se rompió una
> de las Reglas de Oro. El mensaje de error indica exactamente qué tipo y qué regla.

### Ejecutar Frontend (React + Vite)
```powershell
# Backoffice (Puerto 5173)
cd Backoffice-Front
npm run dev

# Venta POS (Puerto 5174)
cd Venta-Front
npm run dev
```

### Migraciones EF Core
Siempre usar los contextos específicos (`NubeContext` o `LocalContext`):
```powershell
# Crear y aplicar migración en la Nube
dotnet ef migrations add <NombreMigracion> --project src/BaresFamilia.Infrastructure --startup-project src/BaresFamilia.Nube.Api --context NubeContext --output-dir Migrations/Nube
dotnet ef database update --project src/BaresFamilia.Infrastructure --startup-project src/BaresFamilia.Nube.Api --context NubeContext

# Crear y aplicar migración en Local
dotnet ef migrations add <NombreMigracion> --project src/BaresFamilia.Infrastructure --startup-project src/BaresFamilia.Local.Api --context LocalContext --output-dir Migrations/Local
dotnet ef database update --project src/BaresFamilia.Infrastructure --startup-project src/BaresFamilia.Local.Api --context LocalContext
```

---

## 🛡️ Reglas y Estándares de Programación

> Las **🔒 Reglas de Oro de Arquitectura** de más arriba tienen prioridad sobre cualquier otra
> consideración de estilo. Lo que sigue las complementa.

### Reglas Generales de Código y Estructura
- **Arquitectura de Carpetas**: Respetar estrictamente la estructura de carpetas y capas establecida (evitar mover archivos fuera de sus respectivas capas o mezclar lógica de infraestructura con la de presentación/modelos).
- **Código Limpio (Clean Code)**: Escribir código limpio, legible, ordenado y auto-documentado. Evitar funciones, métodos o componentes excesivamente largos y acoplados.
- **Nombres Claros**: Usar nombres descriptivos e inequívocos para variables, funciones, métodos y clases que expresen claramente su propósito y lógica (ej. `ObtenerComandasPendientesDeSincronizar` en lugar de `GetPending`).
- **Comentarios**: Comentar el *porqué* de una decisión no obvia (una regla de negocio, un caso borde), no el *qué* hace la línea. Los comentarios existentes que explican decisiones deben conservarse al refactorizar.
- **Idioma**: El dominio se nombra en español (`ObtenerTurnoAbierto`, `ReglaNegocioException`). Las carpetas estructurales del proyecto quedan como están (`Entities`, `Interfaces`, `Services`).

### Backend (.NET 9 + C#)
- **Flujo de DI (Inyección de Dependencias)**: Controller ➔ IService<T> ➔ GenericService<T> ➔ IRepository<T> ➔ GenericRepository<T> ➔ DbContext. Ver Regla de Oro 1.
- **Entidades de Dominio**: Todas deben heredar de `BaseEntity`. Utilizar siempre `Guid` (`UUID`) para evitar colisiones de IDs en sincronizaciones offline.
- **Borrado Lógico**: La propiedad `IsActive` (bool) se usa para borrado lógico. Nunca eliminar filas físicas del catálogo si tienen registros relacionados.
- **Autenticación**:
  - Panel web ➔ JWT Bearer para usuarios (Password/PIN).
  - API Local ➔ Token JWT M2M emitido por la API Nube de larga duración (365 días), inyectado en la cabecera `Authorization: Bearer`.
- **Base de Datos**: Las migraciones se aplican **automáticamente al arrancar** (`Database.Migrate()`) con fallback a `EnsureCreated()`, desde `Infrastructure/Data/InicializadorNube.cs` e `InicializadorLocal.cs`. No es necesario forzar updates en producción de forma manual.
- **Manejo de Tiempos**: Fechas en base de datos almacenadas en UTC. Conversión a local-time solo en vistas de presentación. Npgsql exige `DateTimeKind.Utc` al comparar contra columnas `timestamptz`: los `DateTime?` que llegan por query string vienen como `Unspecified` y hay que normalizarlos (`AsUtc()`).
- **Contratos de Sincronización**: Los DTOs de sync (`Dtos/Sincronizacion/EspejosDeSubida.cs` y `EspejosDeDescarga.cs`) **los comparten las dos puntas**: la sucursal los serializa y la Nube los recibe. Cambiar o renombrar una propiedad rompe la sincronización de las sucursales ya instaladas. Hay pruebas que fijan la forma del JSON (`ContratosDeSincronizacionTests`).
- **La sucursal nunca se bloquea**: en la ingesta de sync, si un dato referenciado todavía no llegó a la Nube, el registro se saltea o se enlaza de forma degradada y se reintenta en el próximo ciclo. **Nunca abortar el lote completo** por un registro.
- **El catálogo local no se siembra**: los métodos de pago, tipos de venta y demás catálogo maestro llegan a la sucursal **solo** por sincronización. Sembrarlos localmente crea filas con Id distinto al de la Nube y rompe el pull por unicidad de nombre. Por eso el POS usa `ICatalogoPosService` (sin siembra) y no los servicios de catálogo de la Nube.


### Frontends (React 19 + TypeScript + Vite + Tailwind CSS v4)
- **Framework & Estilo**: React 19 con componentes funcionales y TypeScript estricto. Uso de Tailwind CSS v4 para estilos (instalado como plugin de Vite).
- **Importaciones**: Usar alias de rutas definidos en tsconfig (ej. `@/components/...` o `@/lib/...`).
- **Peticiones HTTP**: Usar Axios con interceptores preconfigurados (`src/lib/api.ts`). Este interceptor maneja tokens JWT y redirecciones automáticas en caso de `401 Unauthorized`.
- **Backoffice UI ("Modo Claro Corporativo")**:
  - Paleta base: `ice` (fondos) y `pearl` (bordes y textos).
  - Acento: `brand` (azul corporativo).
  - Diseños con transiciones suaves, micro-animaciones táctiles y grids responsivos.
- **Venta POS UI ("Modo Oscuro de Alto Contraste")**:
  - Paleta base: `slate` (fondos y tarjetas oscuras).
  - Acentos: `amber` (dorado/caja) y `cyan` (información).
  - Diseños touch-friendly: botones con altura mínima de 72px, teclado en pantalla (`Numpad`) y modales bloqueantes con backdrop blur.
- **Integración de APIs**:
  - El frontend inicia con arrays de mocks en desarrollo.
  - Al realizar integraciones, los mocks se deben reemplazar por llamadas usando la instancia preconfigurada de axios `api`.

---

## 🤖 Roles del Agente de IA

Cuando trabajes con este repositorio, debés asumir los siguientes roles según corresponda:

1. **Arquitecto de Sistemas (Local-First)**: Asegura que cualquier cambio respete la arquitectura dual y las **🔒 Reglas de Oro**, garantizando la resiliencia offline de la API Local y el control de sincronización de la API Nube.
2. **Desarrollador C# / .NET Senior**: Implementa lógica en backend respetando el flujo de capas establecido (Controller → IServicio → Servicio → IRepositorio → Repositorio), escribiendo código asíncrono optimizado y seguro. Ante la duda entre "resolverlo rápido en el controller" y "respetar las capas", **siempre respetar las capas**.
3. **Ingeniero Frontend (React/TS/Tailwind)**: Diseña y refina interfaces interactivas, manteniendo la consistencia de estilos y garantizando interfaces fluidas en el Backoffice (Claro) y el POS (Oscuro).
4. **Especialista en Integración y Sincronización**: Gestiona flujos complejos de tokens M2M, integraciones de facturación fiscal AFIP simulada e impresión por USB (ESC/POS).
