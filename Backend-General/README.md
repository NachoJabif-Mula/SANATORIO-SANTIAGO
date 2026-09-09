# 🍺 BARES FAMILIA — Sistema de Gestión para Bares

Sistema de gestión integral para cadena de bares con arquitectura dual Nube/Local, diseñado para operar con o sin conexión a internet y sincronizar datos entre sucursales.

---

## 📁 Estructura de la Solución

```
BaresFamilia.sln
└── src/
    ├── BaresFamilia.Nube.Api/          → API centralizada en la nube
    ├── BaresFamilia.Local.Api/         → API local por sucursal
    ├── BaresFamilia.Core.Models/       → Entidades, Enums, Interfaces, Servicios
    └── BaresFamilia.Infrastructure/    → EF Core, DbContexts, Repositorios, DI
```

## 📚 Documentación Detallada

| Documento | Contenido |
|-----------|-----------|
| [NUBE_API.md](./NUBE_API.md) | Documentación completa de la API Nube |
| [LOCAL_API.md](./LOCAL_API.md) | Documentación completa de la API Local |
| [DESPLIEGUE.md](./DESPLIEGUE.md) | Guía de ejecución, despliegue y conexión Nube↔Local |

---

## ⚡ Inicio Rápido

### Prerequisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) o superior
- [PostgreSQL 15+](https://www.postgresql.org/download/) instalado y corriendo
- Git

### 1. Clonar y Restaurar

```bash
git clone <repo-url>
cd Backend-General
dotnet restore BaresFamilia.sln
```

### 2. Crear las bases de datos PostgreSQL

```sql
CREATE DATABASE baresfamilia_nube;
CREATE DATABASE baresfamilia_local;
```

### 3. Ejecutar las APIs

```bash
# Terminal 1 — API Nube (puerto 5280)
dotnet run --project src/BaresFamilia.Nube.Api

# Terminal 2 — API Local (puerto 5044)
dotnet run --project src/BaresFamilia.Local.Api
```

### 4. Acceder a Swagger

- **Nube:** http://localhost:5280/swagger
- **Local:** http://localhost:5044/swagger

---

## 🏛️ Arquitectura

```
┌─────────────────────┐           ┌─────────────────────┐
│   NUBE API          │           │   LOCAL API          │
│   (Cloud/VPS)       │◄─────────►│   (Sucursal)         │
│   PostgreSQL Nube   │   Sync    │   PostgreSQL Local   │
│   Puerto: 5280      │           │   Puerto: 5044       │
└─────────────────────┘           └─────────────────────┘
         │                                   │
         └──────────┬────────────────────────┘
                    │
         ┌──────────▼──────────┐
         │  Core.Models        │
         │  (Entidades, Enums, │
         │   Interfaces)       │
         └──────────┬──────────┘
                    │
         ┌──────────▼──────────┐
         │  Infrastructure     │
         │  (DbContexts, Repos,│
         │   Fluent API, DI)   │
         └─────────────────────┘
```

### Flujo de Inyección de Dependencias

```
Controller → IService<T> → GenericService<T> → IRepository<T> → GenericRepository<T> → DbContext
```

---

## 🛠️ Stack Tecnológico

| Componente | Tecnología | Versión |
|------------|-----------|---------|
| Runtime | .NET | 9.0 |
| ORM | Entity Framework Core | 9.0.15 |
| Base de Datos | PostgreSQL | 15+ |
| Provider DB | Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.4 |
| API Docs | Swashbuckle (Swagger) | 10.1.7 |

---

## 📝 Licencia

Proyecto privado — BARES FAMILIA © 2026
