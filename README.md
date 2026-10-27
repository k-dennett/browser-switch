<p align="center">
  <img src="docs/assets/browserswitch.png" alt="Icono de BrowserSwitch: dos flechas blancas sobre un círculo azul" width="112" height="112">
</p>
<h1 align="center">BrowserSwitch</h1>
<p align="center"><strong>Tus enlaces, tu navegador.</strong><br>Elige dónde abrir cada enlace o cambia de navegador desde la bandeja de Windows.</p>
<p align="center">
  <a href="https://github.com/k-dennett/browser-switch/releases/latest"><img src="docs/assets/badge-release.svg" alt="Descargar BrowserSwitch v1.1.0 para Windows"></a>
  <img src="docs/assets/badge-windows.svg" alt="Windows 10 y 11">
  <img src="docs/assets/badge-dotnet.svg" alt=".NET Framework 4.8">
  <img src="docs/assets/badge-user.svg" alt="Instalación sin administrador">
  <a href="docs/VALIDATION.md"><img src="docs/assets/badge-tests.svg" alt="327 comprobaciones locales aprobadas"></a>
</p>
<p align="center">
  <a href="#-empezar">Empezar</a> · <a href="#-cómo-se-usa">Uso</a> · <a href="docs/WINDOWS.md">Guía de Windows</a> · <a href="docs/VALIDATION.md">Validación</a> · <a href="src/BrowserSwitch/Assets/BrowserSwitch.ico">Icono ICO</a>
</p>

---

## 👀 Así se ve

<table>
  <tr><th>Selector completo · captura real</th><th>Cambio rápido · esquema de la bandeja</th></tr>
  <tr>
    <td width="50%"><img src="docs/assets/selector.jpg" alt="Ventana real de BrowserSwitch con Comet, Google Chrome y Microsoft Edge, modo fijo y Preguntar siempre" width="480"></td>
    <td width="50%"><img src="docs/assets/quick-menu.svg" alt="Esquema del menú de bandeja con Microsoft Edge marcado como navegador activo" width="480"></td>
  </tr>
</table>

La lista se adapta a los navegadores detectados en tu equipo. Puedes agregar otros y personalizar sus argumentos.

## ✨ Qué puedes hacer

| 🔀 Elegir en cada enlace | 📌 Recordar un navegador | 🖱 Cambiar desde la bandeja |
| --- | --- | --- |
| Activa **Preguntar siempre** y decide dónde abrirlo. | Guarda una elección para los siguientes enlaces. | Haz clic en el icono azul y elige otro navegador. |

| Además | Cómo funciona |
| --- | --- |
| 🔎 Detección automática | Consulta navegadores registrados por usuario y por equipo; evita duplicados. |
| 🛠 Entradas manuales | Agrega, edita u oculta navegadores y configura argumentos para perfiles. |
| 👤 Instalación por usuario | Se prepara en tu carpeta local, sin solicitar administrador. |
| 🔗 HTTP, HTTPS y HTML | Conserva el manejo de enlaces y archivos `.htm` / `.html`. |
| 🌙 Residente sin sondeos | Una sola instancia de bandeja, sin temporizadores ni conexiones de red en reposo. |
| 🔒 Preferencias personales | Se guardan fuera del repositorio. No registra enlaces, historial ni cuentas. |

## 🚀 Empezar

**[⬇ Descargar BrowserSwitch para Windows](https://github.com/k-dennett/browser-switch/releases/latest)** · [Notas de v1.1.0](docs/releases/v1.1.0.md)

**Requisitos:** Windows 10/11 con **.NET Framework 4.8** disponible. La instalación gráfica del ZIP no necesita Git, el repositorio ni PowerShell. El repositorio es privado: debes tener acceso para descargar la release.

### 1 · Descargar y extraer

En **Assets** de la release elige **BrowserSwitch-1.1.0-windows.zip** y extrae **todo** su contenido. Los archivos llamados *Source code* contienen el código, no el paquete compilado.

### 2 · Instalar para tu usuario

Abre `BrowserSwitch.exe` desde la carpeta extraída y pulsa **Instalar BrowserSwitch** o **Actualizar BrowserSwitch**. La aplicación comprueba el paquete, cierra el residente anterior, prepara la copia local y arranca automáticamente la bandeja. Al terminar se cierra la ventana del paquete. Si HTTP/HTTPS aún no están asignados, abre Aplicaciones predeterminadas.

En **Configuración → Aplicaciones → Aplicaciones predeterminadas → BrowserSwitch**, asigna **HTTP** y **HTTPS**. Puedes asignar también `.htm` y `.html`.

### 3 · Cambiar desde la bandeja

El icono azul queda disponible en la bandeja para cambiar rápidamente. Los accesos directos del escritorio y del menú Inicio permiten volver a abrir el menú cuando quieras. No necesitas abrirlos para completar la instalación.

Si tienes un editor de navegadores abierto, guarda o cancela ese borrador y vuelve a actualizar. Ante un fallo durante la instalación se intenta recuperar la versión anterior; si la recuperación no se puede completar, se conservan copias y se indica su ubicación.

BrowserSwitch conserva el identificador técnico `SelectorNavegadorPersonal` para mantener asociaciones existentes. La selección se hace con la interfaz normal de Windows; el programa no fuerza `UserChoice` ni sus hashes.

> El paquete incluye `LEEME.txt`, información de versión y scripts opcionales de desinstalación. Compara la descarga con `SHA256SUMS.txt` si quieres comprobar su integridad. El ejecutable todavía no está firmado digitalmente; las políticas del equipo siguen aplicándose.

<details>
<summary><strong>Compilar e instalar desde el código fuente</strong></summary>

Desde la raíz del repositorio, en una consola PowerShell normal para Windows permitida en tu equipo:

```powershell
.\scripts\Build.ps1
.\scripts\Test.ps1
.\scripts\Install.ps1 -OpenSettings
```

El repositorio puede estar en WSL; el programa de Windows se instala en una carpeta local. Si la consola está empaquetada o PowerShell está bloqueado, consulta la [guía de Windows](docs/WINDOWS.md). No se cambian políticas del equipo.

</details>

## 🖱 Cómo se usa

| Quiero… | Acción |
| --- | --- |
| Cambiar el navegador fijo | Clic en el icono de bandeja → nombre del navegador. La marca indica el activo. |
| Elegir cada vez | Bandeja → **Preguntar siempre**. |
| Pegar un enlace | Bandeja → **Abrir ventana…** → pega una dirección completa y elige navegador. |
| Agregar o editar navegadores | Bandeja → **Navegadores…** → realiza cambios → **Guardar**. |
| Iniciarlo al entrar a Windows | Activa **Iniciar con Windows**. Es opcional y viene desactivado. |
| Cerrar el residente | **Salir de la bandeja**. Windows puede seguir abriendo enlaces mediante instancias breves. |

Si el icono está oculto, búscalo dentro de **^** junto al reloj y arrástralo a la zona visible. Cerrar la ventana completa conserva el residente; abrir el acceso directo otra vez reutiliza la misma instancia.

### El recorrido de un enlace

```mermaid
flowchart LR
    A["Enlace desde otra aplicación"] --> B["BrowserSwitch"]
    B --> C{"¿Navegador fijo?"}
    C -->|Sí| D["Abrir en el navegador guardado"]
    C -->|No| E["Mostrar selector"]
    E --> F["Elegir navegador"]
    F --> D
    T["Menú de bandeja"] -. Cambiar preferencia .-> C
```

> **Microsoft Teams:** en sus opciones de enlaces elige **Navegador predeterminado**. Si Teams está configurado para abrir directamente en Edge, puede omitir BrowserSwitch. [Ver detalle](docs/WINDOWS.md#microsoft-teams).

## 📂 Dónde queda todo

```text
Repositorio                         Instalación de Windows
src/                                %LOCALAPPDATA%\BrowserSwitch\
scripts/                            ├── BrowserSwitch.exe
docs/                               ├── BrowserSwitch.exe.config
                                    ├── BrowserSwitch.ico
                                    └── Data\settings.xml
```

El código y la copia instalada están separados. Los binarios generados, accesos directos, diagnósticos y preferencias personales no se suben a Git.

## 🔄 Actualizar o desinstalar

| Actualizar | Desinstalar |
| --- | --- |
| Extrae el ZIP nuevo, abre BrowserSwitch.exe y pulsa **Actualizar BrowserSwitch**. Se conserva tu configuración y la bandeja vuelve a arrancar sola. | Primero elige otro navegador en Windows; después ejecuta `Uninstall.ps1`. Los datos personales se conservan por defecto. |
| [Actualización y recuperación](docs/WINDOWS.md#actualizar) | [Desinstalación y limpieza opcional](docs/WINDOWS.md#desinstalar) |

## 🧩 Proyecto y plataformas

```text
src/BrowserSwitch.Core/    Modelos y validación de enlaces
src/BrowserSwitch/         WinForms e integración Windows
scripts/                  Compilación, empaquetado, publicación, pruebas e iconos
docs/                     Guías, imágenes y resultados de validación
tests/                    Pruebas sin dependencias externas
```

- [x] **Windows:** implementado y validado localmente; [resultados y límites](docs/VALIDATION.md).
- [ ] **macOS:** futuro, con integración nativa propia.
- [ ] **Linux:** futuro, con integración nativa propia.

La lógica compartible está separada de WinForms y del registro. BrowserSwitch es independiente de Browser Picker: se consultó su código público como referencia para el registro por usuario, sin depender de sus ejecutables ni bibliotecas.

### Recursos gráficos

<p><a href="docs/assets/browserswitch.png"><img src="docs/assets/browserswitch.png" alt="Descargar el icono de BrowserSwitch en PNG" width="64" height="64"></a></p>

[PNG · 256 px](docs/assets/browserswitch.png) · [ICO · 16 a 256 px](src/BrowserSwitch/Assets/BrowserSwitch.ico) · [Generador del icono](scripts/Generate-Icon.ps1)

El mismo diseño se utiliza en el ejecutable, la bandeja y los accesos directos. Para regenerar los recursos, ejecuta `scripts/Generate-Icon.ps1`.

Cada cambio en main y cada pull request se compila y prueba en Windows mediante [GitHub Actions](https://github.com/k-dennett/browser-switch/actions/workflows/windows.yml). Las ejecuciones correctas guardan el ZIP y SHA-256 durante 14 días. [Cómo descargar y entender las comprobaciones](docs/CI.md).

Para reproducir y publicar paquetes, consulta la [guía de releases](docs/RELEASING.md).
