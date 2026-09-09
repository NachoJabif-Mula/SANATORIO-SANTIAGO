# BARES FAMILIA — Punto de Venta (POS)

Frontend del sistema de Punto de Venta local para **BARES FAMILIA**. Diseñado para uso táctil en modo quiosco, con **Modo Oscuro de alto contraste** (Gris Pizarra) y preparado para empaquetarse en escritorio con Electron.

## Stack Tecnológico

| Tecnología | Versión | Propósito |
|---|---|---|
| React | 19.x | UI Framework |
| TypeScript | 6.x | Tipado estático |
| Vite | 8.x | Bundler & Dev Server |
| Tailwind CSS | 4.x (v4 con `@tailwindcss/vite`) | Utilidades CSS |
| Lucide React | 1.x | Iconografía |
| React Router DOM | 7.x | Navegación (preparado) |

## Inicio Rápido

```bash
# Instalar dependencias
npm install

# Iniciar servidor de desarrollo
npm run dev

# Build de producción
npm run build
```

El servidor de desarrollo se levanta por defecto en `http://localhost:5173/`.

## Estructura del Proyecto

```
src/
├── App.tsx                     # Root component (Auth gate)
├── main.tsx                    # Entry point
├── index.css                   # Design System (Dark Slate)
├── vite-env.d.ts               # Env types
├── types/
│   └── index.ts                # Domain types (Usuario, Producto, Comanda)
├── data/
│   └── mock.ts                 # Datos simulados de desarrollo
├── contexts/
│   └── AppContext.tsx           # AuthProvider + ComandaProvider
├── components/
│   ├── Numpad.tsx              # Teclado numérico táctil
│   ├── CatalogoPanel.tsx       # Panel izquierdo (categorías + productos)
│   ├── ComandaPanel.tsx        # Panel derecho (comanda activa + cobrar)
│   └── ModalAutorizacion.tsx   # Modal bloqueante de PIN gerente
└── pages/
    ├── LoginScreen.tsx         # Pantalla de bloqueo con PIN
    └── PosScreen.tsx           # Vista principal de toma de pedidos
```

## Usuarios de Prueba

| Nombre | PIN | Rol |
|---|---|---|
| Carlos M. | `1234` | Mozo |
| Ana R. | `5678` | Mozo |
| Diego G. | `0000` | Gerente |

## Funcionalidades Implementadas

### 1. Pantalla de Bloqueo (Login)
- Teclado numérico en pantalla (Numpad) con botones grandes táctiles (72px mínimo)
- Indicadores de PIN con efecto glow ámbar
- Animación de shake en PIN incorrecto
- Branding corporativo con gradientes y efectos visuales

### 2. Vista de Toma de Pedidos
- **Layout 60/40**: Catálogo a la izquierda, comanda activa a la derecha
- **Categorías**: Tragos, Cervezas, Comida, Postres, Sin Alcohol, Cafetería
- **Productos**: Grid touch-friendly con precios en ARS y badge "AGOTADO"
- **Buscador**: Filtro rápido por nombre dentro de la categoría activa
- **Comanda**: Lista de ítems con controles de cantidad (+/-), subtotales y eliminación

### 3. Seguridad — Botón Cobrar
- Si el operador es **Gerente** → procesa directamente
- Si el operador es **Mozo** → lanza modal bloqueante pidiendo PIN de Gerente
- El modal tiene blur de fondo, animación de entrada, y shake en PIN incorrecto
- Confirmación visual con color verde y glow al registrar el cobro

### 4. Design System — Modo Oscuro (Gris Pizarra)
- Paleta: Slate 950→300 para fondos/superficies
- Acento primario: Ámbar (dorado) para acciones y highlights
- Acento secundario: Cyan para elementos informativos
- Alto contraste: Texto `#f1f5f9` sobre fondos `#0a0e17`
- Glassmorphism en modales y overlays
- Animaciones táctiles (scale, shake, glow, fade)

## Preparación para Electron

El proyecto está configurado con `base: './'` en `vite.config.ts` para que los assets sean relativos y funcionen con el protocolo `file://` de Electron. Solo falta agregar el wrapper Electron para empaquetado de escritorio.

## Próximos Pasos

- [ ] Integración con API Local (comanda CRUD, sincronización)
- [ ] Wrapper Electron para empaquetado Windows
- [ ] Impresión de tickets/comanda
- [ ] Gestión de mesas
- [ ] Historial de comandas del turno
