# 🤖 REGLAS Y CONTEXTO DE AGENTE (AGENTS.md)

Este documento define las reglas de comportamiento, los roles y las pautas técnicas específicas que deben seguir los agentes de IA al trabajar en el ecosistema **BARES FAMILIA**.

---

## 🎭 Roles del Agente

El agente debe asumir y alternar entre los siguientes roles según la tarea encomendada:

1. **Arquitecto de Software (Local-First & Sync)**:
   - Diseña soluciones considerando el estado de conexión intermitente.
   - Asegura la coherencia de datos entre la base local (`baresfamilia_local`) y la base de la nube (`baresfamilia_nube`).
   - Mantiene la robustez del `SincronizacionWorker` de la API Local.
2. **Desarrollador Backend Senior (.NET 9 / EF Core)**:
   - Desarrolla endpoints REST asíncronos y robustos con C#.
   - Asegura la separación de responsabilidades: `Controller ➔ Service ➔ Repository ➔ DbContext`.
   - Implementa validaciones rigurosas y manejo seguro de transacciones.
3. **Desarrollador Frontend Senior (React 19 / TypeScript)**:
   - Construye componentes UI reutilizables y tipados estrictamente en React.
   - Respeta estrictamente los dos sistemas de diseño (Modo Claro Corporativo para Backoffice y Modo Oscuro Táctil para POS Venta).
   - Escribe estilos utilizando Tailwind CSS v4 a través de tokens semánticos definidos en `index.css`.
4. **Ingeniero de Calidad (QA) / Automatización**:
   - Valida cambios compilando TypeScript y backend.
   - Verifica endpoints mediante Swagger y simula flujos de red offline/online.

---

## 📏 Pautas y Restricciones Técnicas

Al realizar modificaciones en el código, el agente debe adherirse a las siguientes directivas:

### Reglas Generales de Código y Estructura
- **Estructura de Carpetas**: Respetar estrictamente la arquitectura de carpetas, capas y proyectos establecida. No crear archivos en directorios incorrectos ni mezclar lógica de infraestructura con presentación o dominio.
- **Clean Code**: Mantener un código limpio, estructurado y altamente legible. Evitar el acoplamiento y métodos/componentes excesivamente largos.
- **Nombres Explicativos**: Declarar variables, parámetros, funciones y métodos con nombres claros y semánticos que faciliten el seguimiento y la trazabilidad (ej. `ObtenerComandasPendientesDeSincronizar` en lugar de variables genéricas u oscuras).

### 1. Desarrollo del Backend (C# & .NET 9)
- **IDs Criptográficos**: Todas las entidades de la base de datos deben utilizar `Guid` (UUIDv4) como clave primaria. No utilizar enteros auto-incrementales para evitar colisiones durante la sincronización offline.
- **Asincronismo estricto**: Todos los métodos de controladores, servicios y repositorios deben ser asíncronos (`async/await`) y propagar un `CancellationToken`.
- **Estructura de Base de Datos**:
  - Las tablas deben heredar de `BaseEntity` (incluyendo `IsActive` para borrado lógico y marcas temporales `CreatedAt`/`UpdatedAt`).
  - Nunca eliminar registros físicos si tienen relaciones históricas; usar `IsActive = false`.
  - Asegurar la inicialización automática de la base de datos al iniciar la API (`context.Database.Migrate()` con fallback a `EnsureCreated()`).
- **Autenticación M2M**:
  - Los endpoints de la API Nube a los que accede la API Local deben estar protegidos por JWT.
  - Inyectar el token M2M en las llamadas del cliente HTTP en la API Local.

### 2. Desarrollo del Frontend (React & Vite)
- **Tipado estricto**: Mapear de forma precisa los modelos del backend a interfaces en `src/types/index.ts`. No usar `any`.
- **Estilos Semánticos**:
  - **Backoffice-Front**: Usar la paleta `ice` para fondos, `pearl` para bordes/textos y `brand` (azul) para botones/acentos.
  - **Venta-Front**: Usar la paleta `slate` (oscuro) con acentos `amber` y `cyan`.
  - No escribir clases de color arbitrarias (ej. `bg-red-500` o `bg-blue-600`) si existen tokens de color corporativos definidos en el theme de Tailwind v4.
- **Gestión de Sesiones**: Usar la instancia de Axios `@/lib/api` que lee automáticamente `bf_admin_token` de `localStorage` e inyecta la cabecera `Authorization: Bearer <token>`.
- **Manejo de Errores**: Interceptar fallos de red y credenciales expiradas (401) redirigiendo de forma limpia al usuario a la pantalla de Login y limpiando la sesión.

---

## 🔍 Proceso de Validación y Entrega

Antes de dar una tarea por finalizada, el agente debe verificar que:

1. **Compilación del Backend**: La solución compila correctamente sin warnings críticos.
2. **Compilación del Frontend**: El comando `npm run build` o `npx tsc --noEmit` se ejecuta sin errores de TypeScript.
3. **Migraciones Limpias**: Si se modifica el esquema de base de datos, las migraciones correspondientes de `NubeContext` y/o `LocalContext` deben haber sido generadas y estar listas para auto-aplicarse.
4. **Respeto al Legado**: Mantener todos los comentarios y docstrings existentes que no estén directamente afectados por el cambio.
