# 🍺 BARES FAMILIA — Backoffice en la Nube

Panel de gestión centralizado para la cadena de bares **BARES FAMILIA**. Permite administrar sucursales, catálogo de productos, reportes de ventas y la activación de dispositivos POS desde cualquier navegador.

---

## 📑 Índice

1. [Requisitos Previos](#-requisitos-previos)
2. [Instalación Rápida](#-instalación-rápida)
3. [Variables de Entorno](#-variables-de-entorno)
4. [Conexión con la API Nube](#-conexión-con-la-api-nube)
5. [Stack Tecnológico](#-stack-tecnológico)
6. [Estructura del Proyecto](#-estructura-del-proyecto)
7. [Design System](#-design-system)
8. [Rutas y Vistas](#-rutas-y-vistas)
9. [Componentes de Layout](#-componentes-de-layout)
10. [Integración HTTP (Axios + JWT)](#-integración-http-axios--jwt)
11. [Tipos TypeScript](#-tipos-typescript)
12. [Scripts Disponibles](#-scripts-disponibles)
13. [Build para Producción](#-build-para-producción)
14. [Despliegue](#-despliegue)
15. [Troubleshooting](#-troubleshooting)

---

## 📋 Requisitos Previos

| Herramienta | Versión mínima | Verificar instalación |
|-------------|---------------|----------------------|
| **Node.js** | 18.x o superior | `node --version` |
| **npm** | 9.x o superior | `npm --version` |
| **Git** | cualquier versión | `git --version` |

> **Nota:** No se necesita instalar Tailwind CLI por separado. Tailwind CSS v4 se ejecuta como plugin de Vite automáticamente.

---

## 🚀 Instalación Rápida

```bash
# 1. Clonar el repositorio (si no lo tenés)
git clone <url-del-repo>

# 2. Ir al directorio del frontend
cd Backoffice-Front

# 3. Instalar dependencias
npm install

# 4. Configurar las variables de entorno (ver sección siguiente)
# Editar el archivo .env con la URL de tu API

# 5. Levantar el servidor de desarrollo
npm run dev
```

El servidor se levanta en **http://localhost:5173/** por defecto.

---

## 🔐 Variables de Entorno

El proyecto usa un archivo `.env` en la raíz para configuración. Vite expone al código solo las variables que empiezan con `VITE_`.

### Archivo `.env` (desarrollo)

```env
# URL base de la API Nube (BaresFamilia.Nube.Api)
VITE_API_BASE_URL=http://localhost:5178/api
```

### Archivo `.env.production` (producción)

Creá este archivo para el build de producción:

```env
# URL de la API Nube desplegada
VITE_API_BASE_URL=https://api.baresfamilia.com/api
```

### Variables disponibles

| Variable | Descripción | Valor por defecto |
|----------|-------------|-------------------|
| `VITE_API_BASE_URL` | URL base de la API Nube (sin barra final) | `http://localhost:5178/api` |

> **Importante:** Después de cambiar variables de entorno, reiniciá el servidor de desarrollo (`Ctrl+C` y `npm run dev`).

---

## 🔗 Conexión con la API Nube

Este frontend se conecta al backend **BaresFamilia.Nube.Api** (ASP.NET Core Web API con PostgreSQL). El flujo de conexión es:

```
┌──────────────────┐     HTTP + JWT      ┌──────────────────────┐
│                  │ ◄──────────────────► │                      │
│  Backoffice-Front│                     │  BaresFamilia.Nube.Api│
│  (React + Vite)  │     Axios Client    │  (ASP.NET Core)      │
│  Puerto: 5173    │                     │  Puerto: 5178        │
│                  │                     │                      │
└──────────────────┘                     └──────────┬───────────┘
                                                    │
                                                    │ EF Core
                                                    │
                                               ┌────▼────┐
                                               │PostgreSQL│
                                               │ Nube DB  │
                                               └─────────┘
```

### Paso 1 — Levantar la API Nube

```bash
# Desde el directorio del backend
cd Backend-General

# Ejecutar la API Nube (Puerto 5178 por defecto)
dotnet run --project src/BaresFamilia.Nube.Api
```

La API arrancará en `http://localhost:5178` y ejecutará `Database.Migrate()` automáticamente, creando las tablas en PostgreSQL.

### Paso 2 — Configurar la URL en el frontend

Editar `.env`:

```env
VITE_API_BASE_URL=http://localhost:5178/api
```

### Paso 3 — Levantar el frontend

```bash
cd Backoffice-Front
npm run dev
```

### Paso 4 — Autenticación JWT

El frontend utiliza **JWT Bearer** para autenticarse contra la API. El flujo es:

1. El administrador inicia sesión (endpoint futuro `POST /api/auth/login`)
2. La API devuelve un token JWT
3. El token se almacena en `localStorage` con la clave `bf_admin_token`
4. **Todas las peticiones HTTP** automáticamente inyectan el header `Authorization: Bearer <token>` a través del interceptor de Axios

Para probar sin login (desarrollo), podés setear el token manualmente:

```javascript
// En la consola del navegador (F12)
localStorage.setItem('bf_admin_token', 'TU_JWT_TOKEN_AQUI');
```

### CORS en la API

Si aparecen errores de CORS, asegurate de que el `Program.cs` de la API Nube permita el origen del frontend:

```csharp
// En Program.cs de BaresFamilia.Nube.Api
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173") // URL del frontend
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Después de app.UseRouting() y antes de app.UseAuthorization()
app.UseCors();
```

---

## ⚙️ Stack Tecnológico

| Tecnología | Versión | Rol |
|------------|---------|-----|
| **React** | 19.2.x | Librería de UI |
| **TypeScript** | 6.0.x | Tipado estático |
| **Vite** | 8.0.x | Bundler y dev server |
| **Tailwind CSS** | 4.2.x | Sistema de diseño / estilos |
| **React Router** | 7.15.x | Enrutamiento client-side (SPA) |
| **Axios** | 1.16.x | Cliente HTTP con interceptores |
| **Lucide React** | 1.14.x | Iconografía SVG |
| **ESLint** | 10.x | Linting de código |

---

## 📁 Estructura del Proyecto

```
Backoffice-Front/
├── public/                        ← Archivos estáticos (favicon, etc.)
│   └── vite.svg
├── src/
│   ├── components/                ← Componentes reutilizables
│   │   └── layout/
│   │       ├── AppLayout.tsx      ← Layout raíz: Sidebar + Header + <Outlet/>
│   │       ├── Header.tsx         ← Header contextual con búsqueda y notificaciones
│   │       └── Sidebar.tsx        ← Navegación lateral colapsible
│   ├── lib/                       ← Utilidades y configuración
│   │   └── api.ts                 ← Instancia Axios con interceptores JWT
│   ├── pages/                     ← Vistas/páginas (una por ruta)
│   │   ├── DashboardPage.tsx      ← Panel principal con KPIs y actividad
│   │   ├── CatalogoPage.tsx       ← Tabla interactiva de reportes de ventas
│   │   ├── SucursalesPage.tsx     ← Gestión de sucursales con estado de sync
│   │   └── ActivacionPosPage.tsx  ← Módulo de activación de dispositivos POS
│   ├── types/                     ← Definiciones TypeScript centralizadas
│   │   └── index.ts               ← Interfaces que mapean las entidades del backend
│   ├── App.tsx                    ← Configuración del Router
│   ├── main.tsx                   ← Punto de entrada (mount React en #root)
│   ├── index.css                  ← Design System (Tailwind v4 @theme)
│   └── vite-env.d.ts             ← Tipado de variables de entorno
├── .env                           ← Variables de entorno (desarrollo)
├── index.html                     ← HTML raíz con Google Fonts (Inter)
├── vite.config.ts                 ← Config de Vite: plugins + alias @
├── tsconfig.json                  ← Config TS raíz
├── tsconfig.app.json              ← Config TS aplicación (paths @/*)
├── tsconfig.node.json             ← Config TS para Node (vite.config)
├── eslint.config.js               ← Config ESLint
├── package.json                   ← Dependencias y scripts
└── README.md                      ← Este archivo
```

---

## 🎨 Design System

### Filosofía: "Modo Claro Corporativo"

El diseño sigue una estética corporativa limpia usando dos familias de colores principales:

- **Blanco Hielo** (`ice-50` a `ice-300`): Fondos del layout, áreas de contenido
- **Gris Perla** (`pearl-100` a `pearl-900`): Superficies, bordes, texto en distintas jerarquías

### Paleta de Colores

La paleta se define como **theme tokens** de Tailwind CSS v4 en `src/index.css`:

| Token | Hex | Uso |
|-------|-----|-----|
| `ice-50` | `#f8fafb` | Fondo más claro (hover de filas) |
| `ice-100` | `#f1f4f6` | **Fondo principal del body** |
| `ice-200` | `#e8ecef` | Hover de items de navegación |
| `pearl-200` | `#e4e7eb` | Bordes de inputs y cards |
| `pearl-400` | `#9ca3af` | Texto secundario / labels |
| `pearl-800` | `#1f2937` | **Texto principal del body** |
| `pearl-900` | `#111827` | Títulos y texto enfatizado |
| `brand-500` | `#3b82f6` | **Color corporativo principal** |
| `brand-600` | `#2563eb` | Botones primarios |
| `brand-700` | `#1d4ed8` | Botones hover / gradientes |
| `success-500` | `#10b981` | Estados positivos |
| `warning-500` | `#f59e0b` | Alertas / pendientes |
| `danger-500` | `#ef4444` | Errores / anulaciones |

### Tipografía

- **Font Family:** [Inter](https://fonts.google.com/specimen/Inter) (Google Fonts)
- Cargada en `index.html` con `preconnect` para rendimiento óptimo
- Weights utilizados: 300 (Light), 400 (Regular), 500 (Medium), 600 (SemiBold), 700 (Bold), 800 (ExtraBold)

### Animaciones

Definidas como clases utilitarias en `index.css`:

| Clase | Efecto | Duración |
|-------|--------|----------|
| `.animate-fade-in` | Fade in + slide up (8px) | 0.4s |
| `.animate-slide-in` | Fade in + slide left (16px) | 0.3s |
| `.animate-pulse-glow` | Pulso azul luminoso | 2s (loop) |
| `.stagger-children` | Anima hijos secuencialmente con delay incremental | 0.05s × n |

### Sombras y Radios

| Token | Valor | Uso |
|-------|-------|-----|
| `--shadow-card` | Sutil 1px | Cards en reposo |
| `--shadow-card-hover` | Elevada 4px | Cards en hover |
| `--shadow-sidebar` | Lateral 4px | Sidebar fija |
| `--radius-card` | `0.75rem` | Esquinas de cards |
| `--radius-btn` | `0.5rem` | Esquinas de botones |
| `--radius-badge` | `9999px` | Badges redondeados |

---

## 🗺️ Rutas y Vistas

El enrutamiento está configurado en `src/App.tsx` usando React Router v7:

| Ruta | Componente | Descripción |
|------|-----------|-------------|
| `/` | `DashboardPage` | Panel principal con KPIs de ventas, actividad reciente y estado de sincronización por sucursal |
| `/catalogo` | `CatalogoPage` | **Tabla interactiva de reportes de ventas** con búsqueda, ordenamiento por columna, filtros (estado/sucursal), paginación y exportación |
| `/sucursales` | `SucursalesPage` | Cards de sucursales mostrando estado de sync, última sincronización, cantidad de dispositivos POS y acciones |
| `/activacion-pos` | `ActivacionPosPage` | **Módulo de activación POS**: KPIs (activados/pendientes/JWT M2M), tabla de dispositivos y modal para generar códigos de emparejamiento |

### Vista: Dashboard (`/`)

- **4 stat cards** animados con stagger: Ventas Totales, Comandas Hoy, Ticket Promedio, Sucursales Activas
- **Feed de actividad reciente** (últimas acciones del sistema con timestamp)
- **Panel de sincronización** (estado de cada sucursal con indicador visual)

### Vista: Reporte de Ventas (`/catalogo`)

Tabla completa con las siguientes funcionalidades:

- **9 columnas**: Fecha, Sucursal, Comanda #, Tipo, Ítems, Total, Método Pago, Estado, Sync
- **Ordenamiento**: Click en cualquier header de columna → toggle asc/desc con ícono visual
- **Búsqueda en tiempo real**: Filtra por número de comanda, sucursal o método de pago
- **Filtros dropdown**: Por estado (Cobrada/Abierta/Anulada) y por sucursal
- **Paginación completa**: First/Prev/Next/Last, números de página, selector de filas (10/25/50)
- **Badges semánticos**: Estado (verde=Cobrada, azul=Abierta, rojo=Anulada) y Sync (verde=OK, amarillo=Pendiente, rojo=Conflicto)
- **Total calculado**: Se muestra el total cobrado filtrado en tiempo real
- **Botón Exportar**: Preparado para integrar descarga CSV/Excel

### Vista: Sucursales (`/sucursales`)

- **Cards responsivas** (2 columnas en desktop) para cada sucursal
- Cada card muestra: nombre, dirección, estado (Activa/Inactiva), estado de sync, última sincronización, cantidad de dispositivos POS
- Acciones: Sincronizar manualmente, Editar

### Vista: Activación POS (`/activacion-pos`)

- **3 KPI cards**: Dispositivos activados, pendientes, duración del JWT M2M (365 días)
- **Tabla de dispositivos**: Sucursal, descripción, código alfanumérico, estado, expiración, acción de revocar
- **Botón copiar código**: Click en el ícono de clipboard junto a cada código, con feedback visual (✓ Copiado!)
- **Modal de generación**: 
  - Formulario: Selección de sucursal + nombre del dispositivo
  - Genera un código formato `BAR-XXXX-XXXX` (alfanumérico sin caracteres ambiguos: sin 0/O/1/I/L)
  - Muestra el código generado con botón de copiar y explicación
  - Backdrop blur con cierre al hacer click afuera

---

## 🧩 Componentes de Layout

### `AppLayout.tsx`

Layout raíz que envuelve todas las páginas. Combina:

- `<Sidebar />` a la izquierda (alto fijo, overflow controlado)
- Área principal con `<Header />` sticky arriba y `<Outlet />` scrollable
- Full height (`h-screen`) con `overflow-hidden` en el contenedor principal

### `Sidebar.tsx`

Navegación lateral con las siguientes características:

- **Colapsible**: Botón circular flotante en el borde derecho. Transición suave de 260px → 72px
- **Branding**: Logo con gradiente azul (`brand-500` → `brand-700`) e ícono de copa de vino
- **4 ítems de navegación**: Dashboard, Catálogo, Sucursales, Activación POS
- **Indicador activo**: Background `brand-50` + texto `brand-700` + shadow sutil
- **Avatar del usuario**: Iniciales "AD" en círculo con gradiente, email debajo
- **Responsive**: En modo colapsado, solo muestra íconos centrados

### `Header.tsx`

Barra superior con:

- **Título dinámico**: Se actualiza según la ruta actual usando un mapa de títulos
- **Fecha actual**: Formato largo en español argentino (`es-AR`)
- **Barra de búsqueda global**: Input con ícono, focus ring azul
- **Campana de notificaciones**: Con dot rojo animado (`pulse-glow`)
- **Backdrop blur**: `bg-white/80 backdrop-blur-md` para efecto de transparencia

---

## 🌐 Integración HTTP (Axios + JWT)

### Archivo: `src/lib/api.ts`

Instancia de Axios pre-configurada con interceptores automáticos:

```typescript
import api from '@/lib/api';

// Ejemplo de uso en un componente o servicio:
const response = await api.get('/productos');
const producto = await api.post('/productos', { nombre: 'IPA', ... });
```

### Configuración base

| Propiedad | Valor |
|-----------|-------|
| `baseURL` | `VITE_API_BASE_URL` o `http://localhost:5178/api` |
| `timeout` | 15.000 ms (15 segundos) |
| `Content-Type` | `application/json` |

### Request Interceptor

Antes de cada petición HTTP:

1. Lee el token JWT de `localStorage` (clave: `bf_admin_token`)
2. Si existe, agrega el header `Authorization: Bearer <token>`
3. Si no existe, la petición sale sin header de autenticación

### Response Interceptor

Al recibir un error de la API:

| Código | Acción automática |
|--------|------------------|
| **401** Unauthorized | Elimina el token de `localStorage` y redirige a `/login` |
| **403** Forbidden | Log en consola: "Acceso denegado — permisos insuficientes" |
| Otros errores | Propaga el error para manejo en el componente |

### Almacenamiento del Token

| Clave | Almacenamiento | Descripción |
|-------|---------------|-------------|
| `bf_admin_token` | `localStorage` | JWT del administrador autenticado |

Para guardar el token después del login:

```typescript
localStorage.setItem('bf_admin_token', response.data.token);
```

Para eliminar el token (logout):

```typescript
localStorage.removeItem('bf_admin_token');
```

---

## 📝 Tipos TypeScript

Definidos centralizadamente en `src/types/index.ts`. Mapean directamente las entidades del backend .NET:

### Catálogo

| Interface | Campos principales |
|-----------|-------------------|
| `Sucursal` | id, nombre, direccion, isActive, createdAt, updatedAt |
| `Categoria` | id, nombre, orden, isActive |
| `Producto` | id, categoriaId, nombre, descripcion, colorHex, requiereCocina, isActive |

### Transaccional

| Interface | Campos principales |
|-----------|-------------------|
| `Comanda` | id, sucursalId, mesaId, numero, estado, total, descuento, syncEstado |
| `ComandaItem` | id, comandaId, productoId, cantidad, precioUnitario, notas |
| `Pago` | id, comandaId, metodoPagoId, monto |

### Seguridad

| Interface | Campos principales |
|-----------|-------------------|
| `DispositivoActivacion` | id, sucursalId, codigoActivacion, descripcion, isActivado, expiraEn |

### Tipos Auxiliares

| Tipo | Valores posibles |
|------|-----------------|
| `ComandaEstado` | `'Abierta'` \| `'Cobrada'` \| `'Anulada'` |
| `SyncEstado` | `'Pendiente'` \| `'Sincronizado'` \| `'Conflicto'` |

---

## 📜 Scripts Disponibles

```bash
# Servidor de desarrollo con HMR (Hot Module Replacement)
npm run dev

# Build de producción (type-check + bundle optimizado)
npm run build

# Previsualizar el build de producción localmente
npm run preview

# Ejecutar el linter ESLint
npm run lint
```

---

## 📦 Build para Producción

```bash
# 1. Crear archivo .env.production con la URL de la API en producción
echo "VITE_API_BASE_URL=https://api.baresfamilia.com/api" > .env.production

# 2. Ejecutar el build
npm run build
```

Esto genera la carpeta `dist/` con:

```
dist/
├── index.html           ← HTML minificado
├── assets/
│   ├── index-[hash].js  ← JavaScript bundle (tree-shaken)
│   └── index-[hash].css ← CSS optimizado (Tailwind purged)
└── vite.svg             ← Archivos estáticos
```

El contenido de `dist/` es lo que se despliega en el servidor web.

---

## 🚀 Despliegue

### Opción 1: Servidor estático (Nginx, Apache, Caddy)

```nginx
# nginx.conf
server {
    listen 80;
    server_name backoffice.baresfamilia.com;
    root /var/www/backoffice-front/dist;
    index index.html;

    # SPA fallback: todas las rutas van a index.html
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache agresivo para assets con hash
    location /assets/ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

> **Importante:** Como es una SPA (Single Page Application), el servidor debe redirigir todas las rutas a `index.html`. Sin esto, navegar directamente a `/catalogo` o `/activacion-pos` dará error 404.

### Opción 2: Vercel / Netlify

```bash
# Vercel
npx vercel --prod

# Netlify
npx netlify deploy --prod --dir=dist
```

Ambas plataformas detectan automáticamente que es un proyecto Vite y configuran el SPA fallback.

### Opción 3: Docker

```dockerfile
# Dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

```bash
docker build -t baresfamilia-backoffice .
docker run -p 80:80 baresfamilia-backoffice
```

---

## 🔧 Troubleshooting

### El frontend no se conecta a la API

1. **Verificar que la API esté corriendo:** Abrí `http://localhost:5178/swagger` en el navegador
2. **Verificar la URL en `.env`:** Debe ser `http://localhost:5178/api` (sin barra al final)
3. **Error de CORS:** Agregar la configuración de CORS en el `Program.cs` de la API (ver sección "Conexión con la API Nube")
4. **Reiniciar Vite:** Después de modificar `.env`, reiniciar con `Ctrl+C` y `npm run dev`

### Error: "Module not found: @/..."

El alias `@` está configurado en dos lugares. Ambos deben existir:

- **`vite.config.ts`**: `resolve.alias['@'] = '/src'`
- **`tsconfig.app.json`**: `paths["@/*"] = ["./src/*"]`

### Error al compilar TypeScript

```bash
# Verificar errores de tipo sin compilar
npx tsc --noEmit
```

### La tabla no muestra datos

Los datos de la tabla de ventas (`CatalogoPage`) actualmente usan datos **mock** generados localmente. Para conectar con datos reales, reemplazá el array `mockData` por un `useEffect` que llame a la API:

```typescript
import api from '@/lib/api';

// Dentro del componente:
useEffect(() => {
  api.get('/comandas/reporte').then(res => setData(res.data));
}, []);
```

### El token JWT no se envía

1. Abrir DevTools (F12) → pestaña Application → Local Storage
2. Verificar que exista la clave `bf_admin_token` con un token válido
3. En Network, verificar que el header `Authorization: Bearer ...` aparezca en las peticiones

### Puerto 5173 ya está en uso

```bash
# Opción 1: Matar el proceso que usa el puerto
npx kill-port 5173

# Opción 2: Usar otro puerto
npm run dev -- --port 3000
```
