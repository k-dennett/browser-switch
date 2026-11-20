# BrowserSwitch para macOS

Prototipo local en Swift y AppKit para **macOS 13 Ventura o posterior**. El icono de BrowserSwitch en la barra de menús permite fijar un navegador para los próximos enlaces o activar **Preguntar siempre**. Una marca indica la opción activa. Al abrir la app desde Finder aparece la misma preferencia en una ventana.

BrowserSwitch consulta los navegadores registrados para HTTP y HTTPS, incluidos Safari, Google Chrome y Firefox cuando están disponibles. En modo fijo, el siguiente enlace se envía directamente a la app elegida. Si deja de estar disponible, se muestra el selector. Elegir un navegador en el selector lo usa solo para ese enlace. **Cancelar** o **Esc** descarta ese enlace.

## Compilar e instalar

Usa Xcode 15 o posterior, o sus herramientas de línea de comandos con un SDK de macOS 14 o posterior. Ejecuta estos comandos desde la raíz del repositorio.

```bash
bash macos/build.sh
open macos/build
```

El script genera `macos/build/BrowserSwitch.app` para la arquitectura del Mac que compila (Apple Silicon o Intel). Crea el icono del paquete a partir del [PNG del repositorio](../docs/assets/browserswitch.png), valida el `Info.plist`, aplica una firma ad hoc y muestra el tamaño en disco.

1. Arrastra **BrowserSwitch.app** a **Aplicaciones** y ábrela desde Finder.
2. Ve a **Ajustes del Sistema → Escritorio y Dock → Navegador web predeterminado** y selecciona **BrowserSwitch**. Confirma la elección si macOS la solicita. [Guía de Apple](https://support.apple.com/es-es/102362)
3. Desde el icono de la barra de menús, elige un navegador instalado o **Preguntar siempre**. Para volver a cambiarlo, basta con el menú de BrowserSwitch.

## Volver a otro navegador y desinstalar

1. En **Ajustes del Sistema → Escritorio y Dock → Navegador web predeterminado**, selecciona el navegador que quieres restaurar.
2. En el menú de BrowserSwitch, elige **Salir de BrowserSwitch** y mueve la copia de **Aplicaciones** a la Papelera desde Finder.
3. Para eliminar también la preferencia guardada, ejecuta `defaults delete io.github.k-dennett.BrowserSwitch PreferredBrowserID` si habías elegido un navegador fijo.

## Validación

| Entorno | Resultado |
| --- | --- |
| Windows con .NET Framework | `scripts/Test.ps1` compiló BrowserSwitch y pasaron **327 comprobaciones**. |
| Ubuntu 24.04 en WSL, Swift 6.1.2 | `bash macos/test.sh` pasó **24 comprobaciones**. Cubren HTTP/HTTPS, la conservación de la URL, HTML, la exclusión de BrowserSwitch, el navegador fijo que deja de estar disponible y la persistencia entre procesos. Los casos se adaptaron de las [pruebas de Windows](../tests/BrowserSwitch.Tests.cs). |
| Ubuntu 24.04 en WSL | Se comprobó la sintaxis de todo el código Swift y de los scripts Bash, así como las declaraciones HTTP/HTTPS, HTML/XHTML y el PNG de 256 px. |
| macOS | Pendientes la compilación y el enlace con AppKit, la generación y firma de la `.app`, la medición de su tamaño y la prueba con Launch Services, Finder y Notas. |

### Aceptación pendiente en un Mac

Prepara en **Notas** enlaces con consultas distintas, por ejemplo `https://example.com/?paso=1` a `?paso=6`, tras elegir BrowserSwitch como navegador predeterminado.

| Paso | Resultado esperado |
| --- | --- |
| Elige **Safari** en el menú y pulsa el enlace 1. | Safari lo recibe directamente. El menú marca Safari. |
| Cambia a **Google Chrome** o **Firefox** y pulsa el enlace 2. | El nuevo navegador lo recibe directamente. BrowserSwitch sigue siendo el predeterminado del sistema. |
| Activa **Preguntar siempre**, pulsa el enlace 3 y elige un navegador. | Se abre solo el enlace 3 en el navegador elegido. El menú sigue marcando **Preguntar siempre**. |
| Pulsa el enlace 4 y selecciona **Cancelar**. | Vuelve a aparecer el selector y, al cancelar, no se abre el enlace. |
| Fija un navegador, elige **Salir de BrowserSwitch** y pulsa el enlace 5. | Desaparece primero el icono. macOS relanza BrowserSwitch y entrega el enlace al navegador guardado. |
| Activa **Preguntar siempre**, vuelve a salir y pulsa el enlace 6. | BrowserSwitch vuelve a arrancar y muestra el selector. |
| Abre BrowserSwitch desde Finder sin un enlace. Repite con la app abierta. | La ventana de preferencias aparece en ambos casos y permite cambiar el modo. |
| Fija un navegador de prueba en un volumen extraíble, ciérralo y desmonta el volumen. Pulsa otro enlace. | Aparece el selector con los navegadores que siguen disponibles. |

Para comprobar también HTTP por separado, abre `http://example.com/` desde Notas y observa qué navegador recibe el enlace, incluso si ese navegador convierte después la navegación a HTTPS.
