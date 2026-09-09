# 🔗 BARES FAMILIA — Guía de Integración Frontend ↔ API Nube

Guía paso a paso para conectar el Backoffice Frontend con la API Nube (`BaresFamilia.Nube.Api`).

---

## 📑 Índice

1. [Arquitectura General](#-arquitectura-general)
2. [Prerequisitos](#-prerequisitos)
3. [Paso 1: Levantar PostgreSQL](#paso-1--levantar-postgresql)
4. [Paso 2: Levantar la API Nube](#paso-2--levantar-la-api-nube)
5. [Paso 3: Habilitar CORS en la API](#paso-3--habilitar-cors-en-la-api)
6. [Paso 4: Configurar el Frontend](#paso-4--configurar-el-frontend)
7. [Paso 5: Levantar el Frontend](#paso-5--levantar-el-frontend)
8. [Paso 6: Probar la Conexión](#paso-6--probar-la-conexión)
9. [Flujo de Autenticación JWT](#-flujo-de-autenticación-jwt)
10. [Flujo de Activación POS](#-flujo-de-activación-pos)
11. [Endpoints de la API Nube](#-endpoints-de-la-api-nube)
12. [Manejo de Errores](#-manejo-de-errores)
13. [Entorno de Producción](#-entorno-de-producción)
14. [Diagrama de Flujo Completo](#-diagrama-de-flujo-completo)

---

## 🏗️ Arquitectura General

```
                       INTERNET / LAN
                            │
           ┌────────────────┼────────────────┐
           │                │                │
    ┌──────▼──────┐  ┌──────▼──────┐  ┌──────▼──────┐
    │ Navegador   │  │ Navegador   │  │ Navegador   │
    │ Admin 1     │  │ Admin 2     │  │ Admin N     │
    └──────┬──────┘  └──────┬──────┘  └──────┬──────┘
           │                │                │
           └────────────────┼────────────────┘
                            │
                     ┌──────▼──────┐
                     │ Backoffice  │  ← React + Vite (puerto 5173)
                     │ Frontend    │     Archivos estáticos (SPA)
                     └──────┬──────┘
                            │ HTTP + JWT Bearer
                            │ (Axios interceptors)
                     ┌──────▼──────┐
                     │ API Nube    │  ← ASP.NET Core (puerto 5178)
                     │ .Nube.Api   │     Controllers + Services
                     └──────┬──────┘
                            │ Entity Framework Core
                            │ (Npgsql)
                     ┌──────▼──────┐
                     │ PostgreSQL  │  ← Base de datos centralizada
                     │ Nube DB     │     baresfamilia_nube
                     └─────────────┘

                            ↕ Sincronización (JWT M2M)

    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    │ API Local   │  │ API Local   │  │ API Local   │
    │ Sucursal 1  │  │ Sucursal 2  │  │ Sucursal N  │
    │ (POS)       │  │ (POS)       │  │ (POS)       │
    └─────────────┘  └─────────────┘  └─────────────┘
```

---

## 📋 Prerequisitos

Antes de comenzar, asegurate de tener instalado:

| Herramienta | Versión | Para qué |
|-------------|---------|----------|
| **Node.js** | ≥ 18.x | Frontend (React + Vite) |
| **npm** | ≥ 9.x | Gestor de paquetes |
| **.NET SDK** | 9.x | Backend (API Nube) |
| **PostgreSQL** | ≥ 14.x | Base de datos |
| **Git** | cualquier | Control de versiones |

---

## Paso 1 — Levantar PostgreSQL

### Opción A: Instalación local

```bash
# Verificar que PostgreSQL esté corriendo
psql -U postgres -c "SELECT version();"

# Crear la base de datos (la API la crea automáticamente, pero podés hacerlo manualmente)
psql -U postgres -c "CREATE DATABASE baresfamilia_nube;"
```

### Opción B: Docker

```bash
docker run -d \
  --name baresfamilia-db \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres123 \
  -e POSTGRES_DB=baresfamilia_nube \
  -p 5432:5432 \
  postgres:16-alpine
```

### Verificar conexión

```bash
psql -h localhost -U postgres -d baresfamilia_nube -c "SELECT 1;"
```

---

## Paso 2 — Levantar la API Nube

```bash
# Ir al directorio del backend
cd Backend-General

# Verificar que la connection string sea correcta en appsettings.json
# Archivo: src/BaresFamilia.Nube.Api/appsettings.json
```

El `appsettings.json` de la API debe tener:

```json
{
  "ConnectionStrings": {
    "NubeConnection": "Host=localhost;Port=5432;Database=baresfamilia_nube;Username=postgres;Password=postgres123"
  },
  "Jwt": {
    "SecretKey": "CLAVE_SECRETA_DE_DESARROLLO_MINIMO_32_CARACTERES!!",
    "Issuer": "BaresFamilia.Nube",
    "Audience": "BaresFamilia.Clients",
    "AdminTokenExpirationHours": 8,
    "M2MTokenExpirationDays": 365
  }
}
```

```bash
# Ejecutar la API
dotnet run --project src/BaresFamilia.Nube.Api

# La API arrancará en:
# → http://localhost:5178
# → Swagger UI: http://localhost:5178/swagger
```

> **Auto-Migrate:** Al arrancar, la API ejecuta `context.Database.Migrate()` automáticamente. Las tablas se crean sin necesidad de ejecutar migraciones manualmente.

---

## Paso 3 — Habilitar CORS en la API

Para que el frontend pueda comunicarse con la API desde un dominio/puerto diferente, hay que configurar CORS en el `Program.cs` de la API Nube:

```csharp
// ========================================
// En Program.cs de BaresFamilia.Nube.Api
// ========================================

// Después de builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",    // Vite dev server
                "http://localhost:4173",    // Vite preview
                "https://backoffice.baresfamilia.com" // Producción
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Después de var app = builder.Build(); y antes de app.UseAuthorization();
app.UseCors();
```

> **Importante:** `app.UseCors()` debe ir **antes** de `app.UseAuthentication()` y `app.UseAuthorization()` en el pipeline.

---

## Paso 4 — Configurar el Frontend

```bash
# Ir al directorio del frontend
cd Backoffice-Front

# Instalar dependencias (si no lo hiciste)
npm install
```

### Editar `.env`

```env
# URL base de la API Nube
# IMPORTANTE: incluir /api al final, SIN barra final
VITE_API_BASE_URL=http://localhost:5178/api
```

### Verificar la configuración

La instancia de Axios en `src/lib/api.ts` usa esta variable:

```typescript
const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5178/api',
  timeout: 15000,
  headers: {
    'Content-Type': 'application/json',
  },
});
```

---

## Paso 5 — Levantar el Frontend

```bash
npm run dev
```

Salida esperada:

```
  VITE v8.0.x  ready in XXX ms

  ➜  Local:   http://localhost:5173/
  ➜  Network: use --host to expose
```

Abrir **http://localhost:5173/** en el navegador.

---

## Paso 6 — Probar la Conexión

### Desde el navegador (F12 → Console):

```javascript
// Verificar que la variable de entorno está cargada
console.log(import.meta.env.VITE_API_BASE_URL);
// Debería mostrar: "http://localhost:5178/api"
```

### Desde el navegador (F12 → Network):

1. Interactuar con alguna funcionalidad que haga peticiones HTTP
2. Verificar en la pestaña Network que las peticiones van a `http://localhost:5178/api/...`
3. Verificar que el header `Authorization: Bearer ...` aparece (si hay token)

### Test manual con curl:

```bash
# Verificar que la API responde
curl http://localhost:5178/api/productos

# Si requiere autenticación, primero obtener un token
curl -X POST http://localhost:5178/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@baresfamilia.com","password":"Admin123!"}'
```

---

## 🔐 Flujo de Autenticación JWT

```
┌──────────┐                    ┌──────────┐
│ Frontend │                    │ API Nube │
└────┬─────┘                    └────┬─────┘
     │                               │
     │  POST /api/auth/login         │
     │  { email, password }          │
     │──────────────────────────────►│
     │                               │
     │  200 OK                       │
     │  { token: "eyJhbG..." }       │
     │◄──────────────────────────────│
     │                               │
     │  localStorage.setItem(        │
     │    "bf_admin_token", token)    │
     │                               │
     │  GET /api/productos           │
     │  Authorization: Bearer eyJ... │
     │──────────────────────────────►│
     │                               │
     │  200 OK                       │
     │  [{ id, nombre, ... }]        │
     │◄──────────────────────────────│
     │                               │
     │  ── Si el token expira ──     │
     │                               │
     │  GET /api/cualquier-recurso   │
     │  Authorization: Bearer eyJ... │
     │──────────────────────────────►│
     │                               │
     │  401 Unauthorized             │
     │◄──────────────────────────────│
     │                               │
     │  → Interceptor Axios:         │
     │    1. Borra token             │
     │    2. Redirige a /login       │
     │                               │
```

### Clave localStorage

| Clave | Tipo | Descripción |
|-------|------|-------------|
| `bf_admin_token` | `string` | Token JWT del administrador autenticado |

### Para desarrollo (sin endpoint de login):

```javascript
// En la consola del navegador (F12)
// Pegar un token JWT válido generado por la API
localStorage.setItem('bf_admin_token', 'eyJhbGciOiJIUzI1NiIs...');
```

---

## 📱 Flujo de Activación POS

Este es el flujo completo para vincular un dispositivo POS local con la API Nube:

```
┌──────────┐         ┌──────────┐         ┌──────────┐
│Backoffice│         │ API Nube │         │ POS Local│
│ Frontend │         │          │         │          │
└────┬─────┘         └────┬─────┘         └────┬─────┘
     │                     │                    │
     │ 1. Admin presiona   │                    │
     │    "Generar Código"  │                    │
     │                     │                    │
     │ POST /api/          │                    │
     │ dispositivos/       │                    │
     │ generar-codigo      │                    │
     │ { sucursalId,       │                    │
     │   descripcion }     │                    │
     │────────────────────►│                    │
     │                     │                    │
     │ { codigo:           │                    │
     │   "BAR-7X9P-M2A1", │                    │
     │   expiraEn: 24h }   │                    │
     │◄────────────────────│                    │
     │                     │                    │
     │ 2. Admin copia el   │                    │
     │    código y lo      │                    │
     │    ingresa en el POS│                    │
     │                     │                    │
     │                     │  3. POS envía     │
     │                     │  POST /api/       │
     │                     │  dispositivos/    │
     │                     │  activar          │
     │                     │  { codigo:        │
     │                     │   "BAR-7X9P-M2A1"}│
     │                     │◄──────────────────│
     │                     │                    │
     │                     │  { token: "eyJ.." │
     │                     │    expiresIn:      │
     │                     │    "365 días" }    │
     │                     │──────────────────►│
     │                     │                    │
     │                     │  4. POS almacena   │
     │                     │  el JWT M2M y lo   │
     │                     │  usa para          │
     │                     │  sincronizar       │
     │                     │                    │
```

### Formato del código

```
BAR-XXXX-XXXX

Donde X ∈ {2,3,4,5,6,7,8,9,A,B,C,D,E,F,G,H,J,K,L,M,N,P,Q,R,S,T,U,V,W,X,Y,Z}
(sin 0/O/1/I/L para evitar confusiones visuales)
```

### Reglas del código

| Regla | Valor |
|-------|-------|
| Formato | `BAR-XXXX-XXXX` |
| Caracteres | Alfanuméricos sin ambiguos |
| Expiración | 24 horas desde la generación |
| Uso | Único (se marca como usado al activar) |
| Token resultante | JWT M2M válido por 365 días |

---

## 📡 Endpoints de la API Nube

### Productos (CRUD)

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| `GET` | `/api/productos` | Listar todos los productos activos | JWT |
| `GET` | `/api/productos/{id}` | Obtener producto por ID con categoría | JWT |
| `GET` | `/api/productos/categoria/{categoriaId}` | Listar productos por categoría | JWT |
| `POST` | `/api/productos` | Crear nuevo producto | JWT |
| `PUT` | `/api/productos/{id}` | Actualizar producto existente | JWT |
| `DELETE` | `/api/productos/{id}` | Desactivar producto (borrado lógico) | JWT |

### Dispositivos (Activación POS)

| Método | Ruta | Descripción | Auth |
|--------|------|-------------|------|
| `POST` | `/api/dispositivos/generar-codigo` | Generar código de emparejamiento | JWT |
| `POST` | `/api/dispositivos/activar` | Canjear código por JWT M2M | Anónimo |

### Ejemplo de petición desde el frontend

```typescript
import api from '@/lib/api';

// Listar productos
const { data: productos } = await api.get('/productos');

// Crear producto
const { data: nuevo } = await api.post('/productos', {
  categoriaId: 'uuid-categoria',
  nombre: 'IPA Artesanal',
  descripcion: 'Cerveza IPA de malta importada',
  colorHex: '#F59E0B',
  requiereCocina: false,
});

// Generar código de activación POS
const { data: activacion } = await api.post('/dispositivos/generar-codigo', {
  sucursalId: 'uuid-sucursal',
  descripcion: 'POS Caja Principal',
});
console.log(activacion.codigo); // "BAR-7X9P-M2A1"
```

---

## ⚠️ Manejo de Errores

### Errores HTTP manejados automáticamente (interceptor)

| Código | Comportamiento |
|--------|----------------|
| **401** | Borra `bf_admin_token` de localStorage → Redirige a `/login` |
| **403** | Log en consola: "Acceso denegado" |
| **Otros** | El error se propaga al `catch` del componente |

### Manejo en componentes

```typescript
import api from '@/lib/api';

try {
  const { data } = await api.get('/productos');
  setProductos(data);
} catch (error) {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 404) {
      // No encontrado
    } else if (error.response?.status === 500) {
      // Error del servidor
    }
  }
}
```

---

## 🌍 Entorno de Producción

### Checklist de despliegue

| # | Tarea | Estado |
|---|-------|--------|
| 1 | PostgreSQL configurado y accesible | ☐ |
| 2 | API Nube desplegada (Railway/Azure/VPS) | ☐ |
| 3 | CORS configurado con dominio de producción | ☐ |
| 4 | JWT SecretKey cambiada (≥32 chars, no la de dev) | ☐ |
| 5 | `.env.production` creado con URL de la API | ☐ |
| 6 | `npm run build` ejecutado sin errores | ☐ |
| 7 | Carpeta `dist/` desplegada en hosting estático | ☐ |
| 8 | SPA fallback configurado (todas las rutas → index.html) | ☐ |
| 9 | HTTPS habilitado en ambos dominios | ☐ |

### Variables de entorno por ambiente

| Ambiente | `VITE_API_BASE_URL` |
|----------|---------------------|
| **Desarrollo** | `http://localhost:5178/api` |
| **Staging** | `https://api-staging.baresfamilia.com/api` |
| **Producción** | `https://api.baresfamilia.com/api` |

---

## 🔄 Diagrama de Flujo Completo

```
  ┌─ DESARROLLO ──────────────────────────────────────────────┐
  │                                                            │
  │  Terminal 1:                 Terminal 2:                    │
  │  cd Backend-General          cd Backoffice-Front            │
  │  dotnet run --project        npm run dev                    │
  │  src/BaresFamilia.Nube.Api                                 │
  │       ↓                           ↓                        │
  │  http://localhost:5178       http://localhost:5173          │
  │  (API + Swagger)             (Frontend SPA)                │
  │       ↑                           │                        │
  │       │     Axios HTTP + JWT      │                        │
  │       └───────────────────────────┘                        │
  │                                                            │
  └────────────────────────────────────────────────────────────┘

  ┌─ PRODUCCIÓN ──────────────────────────────────────────────┐
  │                                                            │
  │  api.baresfamilia.com         backoffice.baresfamilia.com  │
  │  (Docker/VPS/Cloud)           (Nginx/Vercel/Netlify)       │
  │  ASP.NET Core                 Archivos estáticos (dist/)   │
  │       ↑                           │                        │
  │       │     HTTPS + JWT Bearer    │                        │
  │       └───────────────────────────┘                        │
  │                                                            │
  └────────────────────────────────────────────────────────────┘
```
