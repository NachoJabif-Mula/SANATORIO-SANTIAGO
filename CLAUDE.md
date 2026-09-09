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
│   └── src/
│       ├── BaresFamilia.Nube.Api/       → API Centralizada (puerto: 5280)
│       ├── BaresFamilia.Local.Api/      → API Local de Sucursal (puerto: 5044)
│       ├── BaresFamilia.Core.Models/    → Modelos de Dominio y Servicios Comunes
│       └── BaresFamilia.Infrastructure/ → Acceso a Datos (EF Core, Repositorios, DI)
├── Backoffice-Front/              → Panel web de administración (React, puerto: 5173)
├── Venta-Front/                   → Terminal de venta táctil (React, puerto: 5174)
└── docker-compose.yml             → DBs Postgres locales de desarrollo
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

### Reglas Generales de Código y Estructura
- **Arquitectura de Carpetas**: Respetar estrictamente la estructura de carpetas y capas establecida (evitar mover archivos fuera de sus respectivas capas o mezclar lógica de infraestructura con la de presentación/modelos).
- **Código Limpio (Clean Code)**: Escribir código limpio, legible, ordenado y auto-documentado. Evitar funciones, métodos o componentes excesivamente largos y acoplados.
- **Nombres Claros**: Usar nombres descriptivos e inequívocos para variables, funciones, métodos y clases que expresen claramente su propósito y lógica (ej. `ObtenerComandasPendientesDeSincronizar` en lugar de `GetPending`).

### Backend (.NET 9 + C#)
- **Flujo de DI (Inyección de Dependencias)**: Controller ➔ IService<T> ➔ GenericService<T> ➔ IRepository<T> ➔ GenericRepository<T> ➔ DbContext.
- **Entidades de Dominio**: Todas deben heredar de `BaseEntity`. Utilizar siempre `Guid` (`UUID`) para evitar colisiones de IDs en sincronizaciones offline.
- **Borrado Lógico**: La propiedad `IsActive` (bool) se usa para borrado lógico. Nunca eliminar filas físicas del catálogo si tienen registros relacionados.
- **Autenticación**:
  - Panel web ➔ JWT Bearer para usuarios (Password/PIN).
  - API Local ➔ Token JWT M2M emitido por la API Nube de larga duración (365 días), inyectado en la cabecera `Authorization: Bearer`.
- **Base de Datos**: Las migraciones se aplican **automáticamente al arrancar** (`Database.Migrate()`) con fallback a `EnsureCreated()`. No es necesario forzar updates en producción de forma manual.
- **Manejo de Tiempos**: Fechas en base de datos almacenadas en UTC. Conversión a local-time solo en vistas de presentación.


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

1. **Arquitecto de Sistemas (Local-First)**: Asegura que cualquier cambio respete la arquitectura dual, garantizando la resiliencia offline de la API Local y el control de sincronización de la API Nube.
2. **Desarrollador C# / .NET Senior**: Implementa lógica en backend usando las capas de inyección de dependencias establecidas, escribiendo código asíncrono optimizado y seguro.
3. **Ingeniero Frontend (React/TS/Tailwind)**: Diseña y refina interfaces interactivas, manteniendo la consistencia de estilos y garantizando interfaces fluidas en el Backoffice (Claro) y el POS (Oscuro).
4. **Especialista en Integración y Sincronización**: Gestiona flujos complejos de tokens M2M, integraciones de facturación fiscal AFIP simulada e impresión por USB (ESC/POS).
