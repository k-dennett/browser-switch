# Validación de la migración a BrowserSwitch

Fecha: 26 de octubre de 2026. Validación en el equipo Windows del usuario, con repositorio en WSL/Ubuntu. Los resultados describen esta sesión; no garantizan otros equipos ni políticas corporativas.

## Realizado

| Comprobación | Resultado y evidencia |
| --- | --- |
| Repositorio e instrucciones | Se inspeccionaron archivos, Git y rutas Linux/Windows. No había AGENTS.md aplicable. Se conservó `.git` y el commit inicial. El fuente original aún no estaba versionado: se archivó una copia en `docs/history`. |
| Rutas anteriores | El acceso directo y el registro apuntaban a `Desktop\ProyectoGPT\SelectorNavegador\SelectorNavegador.exe`; esa ubicación ya no contenía el ejecutable. Se conservaron los archivos antiguos del repositorio. |
| Entorno | Windows con .NET Framework 4.8, Release 533509 y compilador Framework v4 existente. No se instalaron SDK, paquetes ni runtimes y no se solicitó UAC. |
| Compilación principal | `scripts/Build.ps1` compiló desde la ruta UNC WSL, sin advertencias de C#. |
| Pruebas automatizadas | `scripts/Test.ps1`: **286 comprobaciones aprobadas**. Incluyen escape de argumentos con 250 casos generados, un proceso receptor real, validación HTTP/HTTPS/HTML, persistencia, modo fijo/Preguntar siempre, importación única, corrupción, eliminación de entradas, ocultar/restaurar, exclusión del selector y deduplicación. |
| Registro de detección | Prueba con cliente y capacidades temporales en HKCU, con comandos diferentes, eliminados al terminar. En el registro real se detectaron Comet por usuario y Chrome/Edge por equipo, sin repetir ejecutables. |
| Proyecto MSBuild | Compiló usando el alias `\\wsl.localhost\Ubuntu\...`. El MSBuild clásico no acepta `\\wsl$\...` como URI. Emitió MSB3644 por falta de reference assemblies y MSB3270 al recurrir a bibliotecas del runtime; el script principal evita esas advertencias. |
| Virtualización | Se detectó una primera copia bajo el LocalCache privado del anfitrión empaquetado. Se añadió rechazo de identidad de paquete a instalación/registro. La instalación final se lanzó mediante la instancia existente del Explorador, fuera del paquete. |
| Instalación real | Existencia física comprobada desde `/mnt/c/Users/.../AppData/Local/BrowserSwitch`. Diagnóstico ejecutado por la copia local, registro y accesos directos apuntando a `BrowserSwitch.exe`. Actualización repetida con preferencias conservadas y respaldos `.previous`. |
| Correspondencia al completar la migración inicial | SHA-256 del ejecutable compilado y del instalado físicamente: `ac28f187bdba66e60e61f1b2a23f3c005d63d7ba2d261c1972cfee2871d97008`. |
| Asociaciones | El diagnóstico fuera del paquete informó HTTP y HTTPS activos con `SelectorNavegadorPersonal`. Se conservó el ProgID y se cambió el nombre visible a BrowserSwitch. No se escribió UserChoice, sus hashes, HKLM ni políticas. |
| Interfaz instalada | Se abrió desde el nuevo acceso directo del escritorio y se revisaron selector, estado, lista y editor. Se editó el nombre de un navegador detectado en el borrador, se verificó que pasa a Manual y se canceló sin guardar la prueba. |
| Preguntar siempre | Se activó desde la interfaz. Un enlace HTTPS de `example.com` abierto por el Explorador llegó al selector instalado con la dirección correcta. |
| Elección y recuerdo | En ese selector se marcó recordar y se eligió Google Chrome. Se comprobó la pestaña `https://example.com/?browserswitch=ask-test`. |
| Modo fijo | Un segundo enlace HTTP abierto por el Explorador llegó directamente a Chrome. Se comprobó la pestaña `https://example.com/?browserswitch=fixed-test` —Chrome actualizó HTTP a HTTPS— y el selector no quedó abierto para ese enlace. |
| Preferencia final | Se restauró Microsoft Edge desde la interfaz, igual que el `preferencia.txt` original. El XML físico conservó ese identificador tras actualizar la copia final. No quedaron entradas manuales de prueba. |
| Accesos directos | Se inspeccionaron los destinos de BrowserSwitch en escritorio/Inicio y Cambiar navegador en escritorio/repositorio. Se ejecutó el nuevo acceso del escritorio; también se abrió el antiguo Cambiar navegador después de actualizarlo. Todos apuntan a la ubicación estable. |
| Exclusiones Git | Verificado `git check-ignore` para binarios, obj, preferencias y accesos directos. `git diff --check` sin errores. |

## Límites y pruebas pendientes

- **Aplicaciones predeterminadas:** el botón abrió Configuración de Windows. La autorización del control de interfaz caducó antes de poder inspeccionar esa ventana; no se realizó una nueva selección desde esa interfaz. HTTP y HTTPS ya estaban asignados al mismo ProgID antes de migrar y se comprobaron funcionalmente después, sin forzar asociaciones.
- **PowerShell del sistema:** AppLocker rechazó `powershell.exe` (evento 8004). No se alteró ni eludió esa política ni se volvió a intentar ejecutar ese programa tras identificar el bloqueo. La instalación final usó la preparación integrada de BrowserSwitch, que sí pudo ejecutarse normalmente. `Install.ps1` y `Uninstall.ps1` se revisaron sintácticamente; el flujo completo del wrapper de instalación no se pudo ejecutar desde una consola normal permitida en este equipo.
- La desinstalación completa no se ejecutó porque retiraría la aplicación que se acaba de preparar. Debe comprobarse después de elegir otro navegador en Windows. El registro se niega a desinstalar mientras las asociaciones admitidas sigan activas.
- El alta/edición/borrado persistente de configuraciones manuales se comprobó en lógica y almacenamiento; no se completó cada combinación del editor gráfico con guardado real. La edición visual se verificó como borrador y se descartó.
- No se abrieron enlaces reales de Teams ni se modificaron sus opciones. Está documentado el ajuste Navegador predeterminado.
- No se verificaron DDE, lanzadores de navegadores empaquetados, todas las marcas posibles, equipos ARM64, distintos niveles de DPI, otros usuarios ni instalaciones Windows diferentes. Los comandos que no se puedan resolver de forma segura requieren una entrada manual.
- macOS y Linux quedan como trabajo futuro. WSL solo aloja el código; la aplicación validada es Windows.

Los diagnósticos completos, instantáneas de registro, preferencias y respaldos de accesos permanecen en la carpeta personal local, fuera del repositorio compartible. No se guardó historial de navegación en la aplicación.
## Ampliación: residente en la bandeja

La copia local instalada incorpora ahora NotifyIcon, menú de navegador fijo/Preguntar siempre, ventana reutilizable y arranque por usuario opcional. El residente no usa temporizadores ni sondeos; usa el bucle de mensajes de WinForms y señales de activación. Las opciones de enlace siguen ejecutándose en procesos breves independientes.

- Compilación final y las **286 comprobaciones existentes** aprobadas después de añadir la bandeja.
- Copia compilada e instalada físicamente con el mismo SHA-256: `9ba9b670f005cb2a1aa0b45d80c161f683362d9490e3c7746f6c3c00acb60835`.
- Se abrió la ventana mediante `--settings`, se comprobó el icono propio de dos flechas en esa ventana y se cerró conservando el residente.
- Se repitió la apertura del acceso directo y de `--tray`: quedó un único proceso residente, sin ventanas, y se conservó el mismo PID en el segundo inicio silencioso.
- Muestra de ejecución limpia sin ventana: **35,68 MiB de working set**, **0 segundos adicionales de CPU durante 30,25 segundos**. Es una muestra local, no una garantía de uso constante; abrir ventanas y menús incrementa memoria y CPU.
- Se conservó Microsoft Edge como navegador fijo. No se activó el inicio con Windows: queda disponible en el menú a elección del usuario.
- El control de interfaz no expone directamente la bandeja ni el menú emergente sin ventana. Se comprobó la activación y la instancia única; la selección de cada opción del menú por clic y el arranque tras reiniciar sesión quedan pendientes de comprobación visual completa. Las acciones de preferencias y apertura de enlaces mantienen las pruebas anteriores.
- La opción de inicio usa únicamente el valor propio en HKCU Run. No se probaron cambios de esa opción ni un reinicio de sesión durante este trabajo.

Referencia del componente Windows empleado: [NotifyIcon y menú contextual de WinForms](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-associate-a-shortcut-menu-with-a-windows-forms-notifyicon-component).
## Corrección: botón vacío al abrir el menú de bandeja

La captura del usuario mostró que abrir el menú con clic izquierdo producía además un botón sin icono en la barra de tareas. La presentación manual del ContextMenuStrip no lo declaraba como ventana auxiliar.

Se reprodujo con una prueba que crea y muestra el menú nativo y consulta sus estilos reales: fallaba porque no tenía `WS_EX_TOOLWINDOW`. El menú específico de BrowserSwitch aplica ese estilo y elimina `WS_EX_APPWINDOW` antes de crear su ventana; se utiliza tanto en la apertura manual como en la del NotifyIcon.

Resultado: **294 comprobaciones aprobadas**, incluidas ocho verificaciones al abrir y cerrar dos veces el popup: permanece visible mientras está abierto, no solicita botón de aplicación, está excluido de barra de tareas/Alt+Tab por su estilo auxiliar y se oculta correctamente al cerrar. La corrección no cambia el navegador fijo ni las asociaciones de Windows. La comprobación de regresión consulta el popup real, no solo valores simulados.

Referencia: [reglas de Windows para los botones de la barra de tareas](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar#managing-taskbar-buttons).
## Corrección: icono genérico en la búsqueda de Windows

El icono anterior solo se creaba al ejecutar el programa; el compilador no recibía un icono para el `.exe`. Se añadió un único archivo ICO propio con siete resoluciones (16, 24, 32, 48, 64, 128 y 256 píxeles), generado reproduciblemente desde el mismo diseño de dos flechas.

El proyecto y Build.ps1 incorporan ese archivo tanto como icono nativo de Windows como recurso administrado para la bandeja. La instalación extrae BrowserSwitch.ico junto al ejecutable, actualiza IconLocation en los accesos directos y emite SHCNE_UPDATEITEM para esas rutas; no borra la caché global ni reinicia el Explorador.

- **299 comprobaciones aprobadas**, incluidas carga del icono a 32 px, conservación del azul, siete resoluciones en el ICO y enumeración nativa de los recursos RT_GROUP_ICON/RT_ICON dentro del `.exe` sin ejecutarlo.
- SHA-256 del binario compilado e instalado físicamente: `71765473beb4b305fffc457f22ab9705094667d46644855d4e8f318b50d90a16`.
- SHA-256 del ICO fuente y de la copia instalada: `d1d39fc7c33c1256af4e321f234e9722f5cd38783217b19db2f0cda7e8da72b1`.
- El acceso del escritorio y el archivo real del menú Inicio (leído desde el sistema de archivos Windows mediante WSL para evitar virtualización) apuntan a `%LOCALAPPDATA%\BrowserSwitch\BrowserSwitch.ico,0`.
- Se reinició el residente corregido. La actualización no cambia preferencias ni asociaciones.
- No se confirmó visualmente el resultado dentro del buscador de Inicio; la búsqueda puede conservar el resultado anterior hasta cerrar y abrir su panel de nuevo. Se verificaron el binario, el recurso y el acceso que Windows utiliza.

Referencia de la actualización dirigida: [notificar al shell un cambio del icono de un acceso directo](https://devblogs.microsoft.com/oldnewthing/20150903-00/?p=91671).
## Release descargable v1.0.0 — 27 de octubre de 2026

Se publicó [BrowserSwitch v1.0.0 para Windows](https://github.com/k-dennett/browser-switch/releases/tag/v1.0.0), desde el commit `4eb54f75d34f8069712b5fc79d373bd62bfc625a`, con dos archivos: `BrowserSwitch-1.0.0-windows.zip` y `SHA256SUMS.txt`. El repositorio conserva su visibilidad privada.

- Compilación desde el repositorio y **299 comprobaciones aprobadas** antes de generar el paquete.
- Contenido del ZIP limitado a ejecutable, configuración de runtime, icono, instrucciones, notas, versión, manifiesto y dos scripts opcionales de desinstalación. Sin preferencias, accesos directos ni datos personales.
- Extracción nueva en una ruta con espacios, comparación exacta del contenido y los hashes, comprobación de FileVersion y ejecución de `--diagnostics` desde ese ejecutable extraído, sin requerir archivos fuente para funcionar.
- GitHub confirmó ambos archivos, sus tamaños y hashes. Después de publicar se descargó de nuevo el ZIP mediante la API autenticada y su SHA-256 coincidió: `ec2f611cd901a1e11aaceb1c236fc4c5df261b20de06b9e5870459418c2c0170` (64 366 bytes).
- Se verificó que v1.0.0 está publicada y es la release más reciente. Los enlaces finales de descarga utilizan la etiqueta v1.0.0.

Esta comprobación no realizó una instalación limpia en otro Windows ni repitió el recorrido gráfico de instalación sobre el equipo actual. Siguen pendientes las pruebas de otros equipos, inicio tras reiniciar sesión y desinstalación completa. El ejecutable todavía no tiene firma digital.
## Instalación y actualización automáticas v1.1.0 — 27 de octubre de 2026

- **327 comprobaciones automatizadas aprobadas**: se añadieron recuperación tras una sustitución parcial, destino bloqueado y fallo de confirmación final (incluidos los respaldos `.previous`); restauración de tipos, subclaves y alias del registro propio; arranque real de la bandeja, instancia única, cierre coordinado, compatibilidad con residente antiguo inactivo y rechazo del cierre mientras hay un editor modal.
- Actualización gráfica real mediante **Actualizar BrowserSwitch**, con el residente v1.0 todavía funcionando: quedó un único proceso desde la ubicación local, arrancado automáticamente. La ventana del paquete desapareció sin abrir manualmente el acceso directo.
- Se repitió la actualización sobre el nuevo residente: salió el proceso previo, apareció uno nuevo local y se volvió a cerrar la ventana del paquete. No se obtuvo un código de salida fiable del monitor del proceso ajeno; el cierre coordinado con código cero se verifica por separado en las pruebas con procesos hijos.
- Se comparó el `settings.xml` físico mediante WSL antes y después de ambas actualizaciones: su SHA-256 permaneció idéntico y se conservó Microsoft Edge como navegador fijo. El diagnóstico local mantuvo HTTP y HTTPS activos con el mismo ProgID.
- La primera actualización conservó el ejecutable v1.0 como `.previous`, con el mismo hash registrado antes de iniciar. La actualización siguiente conserva como anterior la versión instalada inmediatamente previa.
- No se escribió UserChoice, no se cambiaron políticas ni se activó el inicio con Windows. No se instalaron runtimes o SDK ni se solicitó administrador.

Las pruebas de recuperación provocan fallos en archivos y claves HKCU aislados de prueba; no se dañó intencionadamente la instalación del usuario. La instalación limpia en otro Windows, el inicio tras reiniciar sesión y la desinstalación completa siguen pendientes.
