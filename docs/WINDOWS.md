# Guía de Windows

[← Volver a BrowserSwitch](../README.md)

Selector de navegador por usuario para Windows. Conserva la herramienta original en C# y Windows Forms: Windows entrega un enlace a BrowserSwitch y puedes elegir dónde abrirlo, recordar un navegador o **Preguntar siempre**. También admite enlaces pegados y archivos `.htm`/`.html`.

Es independiente de Browser Picker. Su código público se consultó como referencia para el registro por usuario; BrowserSwitch no usa sus ejecutables, bibliotecas ni servicios.

## Instalar desde la release descargable

Descarga el ZIP compilado desde [Releases](https://github.com/k-dennett/browser-switch/releases/latest), extrae todo y abre BrowserSwitch.exe. Pulsa **Instalar BrowserSwitch**, selecciona HTTP y HTTPS en Aplicaciones predeterminadas y abre después el acceso directo del escritorio o Inicio. No necesitas el repositorio, Git ni PowerShell para esta instalación gráfica.

Para actualizar, primero usa **Salir de la bandeja**. La carpeta extraída contiene instrucciones y scripts opcionales de desinstalación. No ejecutes el programa directamente desde dentro del ZIP. El binario no está firmado digitalmente y no se modifican restricciones de la organización.

## Requisitos

- Windows 10/11 con **.NET Framework 4.8** ya instalado. El programa tiene un manifiesto `asInvoker`: no solicita administrador.
- Para compilar: PowerShell de Windows 5.1 o PowerShell 7 para Windows, y el compilador `csc.exe` de .NET Framework que ya trae este entorno. No requiere Visual Studio, SDK moderno, NuGet ni descargas.
- La política de la organización debe permitir ejecutar BrowserSwitch. No se modifican políticas, controles de seguridad ni asociaciones protegidas.

El código se limita a la superficie compatible de .NET Framework 4.0/C# 5, ejecutada sobre 4.8. El proyecto clásico puede abrirse en Visual Studio; `Build.ps1` usa directamente el compilador y las bibliotecas de Windows para evitar exigir un targeting pack adicional.

## Estructura

```text
BrowserSwitch.sln
src/BrowserSwitch.Core/       Modelos, preferencias y validación de enlaces
src/BrowserSwitch/            Proyecto y formularios WinForms
src/BrowserSwitch/Windows/    Detección, argumentos, registro, datos e instalación
scripts/                     Build.ps1, Install.ps1, Test.ps1, Uninstall.ps1
tests/                      Pruebas sin dependencias externas
docs/history/               Copia del fuente original antes de la migración
docs/VALIDATION.md           Comprobaciones y límites de la validación local
```

La lógica de `BrowserSwitch.Core` se compila como fuentes enlazadas en el ejecutable; no depende del registro ni de WinForms. Es el punto de partida para compartir reglas y modelos. **macOS y Linux son futuros**: requieren interfaces, descubrimiento, almacenamiento e integración propios; en esta etapa solo se implementa Windows. No se promete que WinForms funcione como aplicación Linux mediante WSL.

La carpeta local antigua `SelectorNavegador/` se conserva como respaldo y queda ignorada por Git. No forma parte del nuevo proyecto. Su fuente inicial se conserva en `docs/history/SelectorNavegador.cs.txt` porque todavía no estaba versionado al iniciar esta migración. No se reemplazó `.git` ni se reescribió su historial.

## Rutas: código y programa instalado

El repositorio puede estar en un disco Windows o en WSL. Por ejemplo, `/home/TU_USUARIO/projects/personal/browser-switch` en Linux corresponde a `\\wsl$\Ubuntu\home\TU_USUARIO\projects\personal\browser-switch` en Windows; `wslpath -w "$PWD"` muestra el equivalente real.

Windows ejecuta una copia **local y estable**:

```text
%LOCALAPPDATA%\BrowserSwitch\BrowserSwitch.exe
%LOCALAPPDATA%\BrowserSwitch\BrowserSwitch.exe.config
%LOCALAPPDATA%\BrowserSwitch\BrowserSwitch.ico
%LOCALAPPDATA%\BrowserSwitch\Data\settings.xml
```

No registres el ejecutable de WSL, `artifacts/`, una carpeta temporal ni una ruta de ProyectoGPT. El registro comprueba la ubicación estable. Puedes mover el repositorio después de instalar; los accesos directos no dependen de él.

**Entornos empaquetados:** algunas aplicaciones, incluido el anfitrión de esta sesión, virtualizan `LOCALAPPDATA`. El instalador y el registro rechazan procesos con identidad de paquete. Inicia la instalación desde el Explorador de Windows o una consola normal permitida, fuera del anfitrión empaquetado. En WSL puedes comprobar la existencia física de la copia en `/mnt/c/Users/TU_USUARIO/AppData/Local/BrowserSwitch`; no basta con que una consola virtualizada afirme que existe.

## Compilar, probar e instalar

Desde PowerShell para Windows, situado en la raíz de este repositorio:

```powershell
.\scripts\Build.ps1
.\scripts\Test.ps1
.\scripts\Install.ps1 -OpenSettings
```

El icono multirresolución de `src/BrowserSwitch/Assets/BrowserSwitch.ico` está incluido como recurso nativo del ejecutable y como recurso compartido por la bandeja. La instalación extrae una copia local para los accesos directos y notifica a Windows su actualización. Para regenerarlo desde las mismas flechas dibujadas por código, usa `scripts/Generate-Icon.ps1`; el archivo `.ico` generado se versiona.

`Build.ps1` admite `-OutputDirectory` y produce por defecto `artifacts\windows\BrowserSwitch.exe` y su `.config`. Lee la lista de fuentes y referencias de `BrowserSwitch.csproj`. El MSBuild clásico también compila con el alias UNC `\\wsl.localhost\Ubuntu\...`; no acepta el host `wsl$`. Sin targeting pack instalado emite advertencias al resolver referencias desde el runtime: el procedimiento recomendado y comprobado sin esas advertencias es `Build.ps1`. `Test.ps1` compila pruebas, ejecuta un receptor de argumentos local y crea claves de prueba propias en HKCU, que elimina en `finally`; no cambia los predeterminados.

Si el código está en WSL, abre PowerShell para Windows en la ruta UNC del repositorio y ejecuta los mismos scripts. Desde Linux, si la interoperabilidad y la política de tu equipo lo permiten, puedes invocar:

```bash
powershell.exe -NoProfile -File "$(wslpath -w "$PWD/scripts/Build.ps1")"
```

En el equipo donde se validó esta migración, AppLocker bloquea `powershell.exe`: esa invocación no se pudo usar. La compilación se realizó con el PowerShell y el compilador ya disponibles en el entorno de trabajo. No se desactivó AppLocker ni se cambiaron políticas.

**Instalar sin una consola:** una vez compilado, abre `artifacts\windows\BrowserSwitch.exe` desde el Explorador y pulsa **Instalar BrowserSwitch**. Esto prepara la copia local y abre Aplicaciones predeterminadas. La opción `--install` realiza la misma preparación y permite automatizarla desde un proceso Windows normal permitido. Ninguna de estas opciones necesita ejecutar PowerShell.

La preparación:

1. Guarda una instantánea del registro propio y respaldos de accesos directos en `Data\Migration`.
2. Copia los binarios y ejecuta un diagnóstico desde la ubicación local antes de cambiar destinos. En actualizaciones conserva el ejecutable anterior como `BrowserSwitch.exe.previous`.
3. Importa una sola vez `preferencia.txt`: primero junto al ejecutable anteriormente registrado y, si no existe, desde la copia antigua del repositorio. Si ya existe `settings.xml`, lo respeta. Un navegador ausente vuelve a preguntar.
4. Registra únicamente HKCU y crea **BrowserSwitch** en el escritorio y el menú Inicio. Actualiza los accesos existentes **Cambiar navegador** del escritorio y del repositorio cuando apuntan al selector antiguo.

No borra el programa anterior. Comprueba la nueva copia antes de retirar respaldos por tu cuenta.

## Activación en Windows

Pulsa **Configurar en Windows** y elige BrowserSwitch en **Configuración → Aplicaciones → Aplicaciones predeterminadas**. Revisa **HTTP y HTTPS**; puedes asignar también `.htm` y `.html`.

- Nombre del producto, proyecto, ejecutable y registro visible: **BrowserSwitch**.
- Identificador técnico/ProgID conservado: **SelectorNavegadorPersonal**.
- Claves propias: `HKCU\Software\Classes\SelectorNavegadorPersonal`, `HKCU\Software\Clients\StartMenuInternet\SelectorNavegadorPersonal` y el valor `BrowserSwitch` de `HKCU\Software\RegisteredApplications`.

Conservar el ProgID mantiene válidas las asociaciones existentes. Si ya estaban activas, no necesitas elegir otro navegador y volver. El programa solo **lee** `UserChoice` para informar del estado: no escribe sus valores ni sus hashes, no aplica asociaciones por políticas y no escribe HKLM.

La URL de Configuración usa `ms-settings:defaultapps?registeredAppUser=BrowserSwitch`. Si tu versión de Windows abre la lista general, busca BrowserSwitch manualmente.

## Uso

### Cambio rápido desde la bandeja

La copia instalada se queda en la bandeja de Windows con un icono azul de dos flechas. Haz clic izquierdo o derecho para mostrar el menú: el navegador activo lleva una marca, y puedes cambiarlo con un solo clic o elegir **Preguntar siempre**. Seleccionar un navegador en este menú cambia los próximos enlaces; no abre una página por sí solo.

**Abrir ventana…** conserva el selector completo para pegar enlaces. Cerrar esa ventana deja el icono activo; **Salir de la bandeja** termina el residente. Los enlaces siguen funcionando aunque salgas: Windows inicia una instancia breve de BrowserSwitch para cada enlace y lee la misma preferencia.

**Iniciar con Windows** es opcional y está desactivado de fábrica. Solo modifica el valor propio `BrowserSwitch` en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, apuntando a la copia estable con `--tray`. La desinstalación retira ese valor. No se instala un servicio ni una tarea programada y no se pide administrador.

Si Windows oculta el icono, búscalo en la flecha de iconos ocultos; puedes arrastrarlo a la zona visible de la bandeja. Windows administra esa posición. Abrir el acceso directo otra vez muestra el menú de la misma instancia; no crea residentes adicionales.

El residente no utiliza temporizadores, sondeos, conexiones de red ni vigilancia de archivos: descubre navegadores y carga preferencias al interactuar. Mantiene memoria de .NET/WinForms incluso en reposo. Para cero memoria residente puedes usar **Salir de la bandeja** y abrir el acceso directo cuando quieras cambiar.

Opciones adicionales: `--tray` inicia la bandeja sin desplegar el menú (usada al iniciar sesión); `--settings` abre o reutiliza la ventana del residente. El modo de bandeja solo se inicia desde la copia instalada local. El ejecutable del repositorio mantiene su ventana de preparación para instalarlo.

- Abre un enlace desde otra aplicación: elige un navegador. Marca **Usar esta elección para los próximos enlaces** para activar el modo fijo.
- En **Abrir ventana…**, deja el campo vacío y pulsa un navegador para fijarlo. O pega una dirección completa y pulsa el navegador para abrirla.
- Pulsa **Preguntar siempre** para borrar la elección fija. Con un enlace pendiente, todavía debes elegir dónde abrirlo.
- Las teclas `1` a `9` eligen navegadores cuando no estás escribiendo en el campo; `Esc` cierra la ventana.
- Si el navegador fijo ya no existe o no puede iniciarse, se informa del fallo y aparece el selector para elegir otro.

### Navegadores detectados y manuales

**Navegadores…** permite agregar, editar y quitar entradas. Los cambios solo se guardan con **Guardar**. Puedes examinar un `.exe` y agregar argumentos fijos opcionales para perfiles u otras opciones del navegador. No incluyas `%1`, `%L`, `%*` ni comandos de shell: el enlace se agrega como argumento separado al final.

Se consultan `StartMenuInternet` y las capacidades HTTP/HTTPS de `RegisteredApplications` en HKCU y HKLM, en vistas de 32 y 64 bits. Se da prioridad a las entradas del usuario y se descartan registros sin un ejecutable utilizable. La detección ofrece una entrada por ruta de ejecutable, aunque Windows publique varios comandos para el mismo navegador. Conserva Chrome, Edge y Comet por rutas conocidas como alternativa.

Editar uno detectado crea una personalización y oculta el original; quitarlo solo lo oculta, no desinstala el navegador ni cambia su registro. **Restaurar detectados** vuelve a mostrarlos. Las entradas manuales permiten perfiles distintos; las que tienen la misma ruta y argumentos se combinan. Se excluyen BrowserSwitch y el selector antiguo para evitar recursión. Registros con lanzadores especiales, DDE o marcadores incrustados requieren una entrada manual con el ejecutable real.

Los navegadores se inician mediante `ProcessStartInfo` con `UseShellExecute=false`. Se escapan las comillas y barras inversas para Windows y la dirección es siempre un argumento propio; no se concatena a código de `cmd` o PowerShell. Solo se aceptan HTTP, HTTPS y archivos HTML.

### Datos personales

`Data\settings.xml` contiene el navegador fijo, las entradas manuales y las detectadas ocultas. No se guardan enlaces, historial, cuentas ni contraseñas. Las rutas personales y los diagnósticos permanecen fuera del código versionado.

El guardado usa archivo temporal, sustitución atómica y `settings.xml.bak`. Las escrituras se serializan por usuario. Al volver a una ventana se recarga la configuración. Entre ediciones simultáneas prevalece el último guardado. Si el XML está dañado, se conserva como `settings.xml.corrupt-*`, se avisa y se vuelve a preguntar.

### Microsoft Teams

Teams puede abrir enlaces directamente en Edge por una preferencia propia. En sus opciones de archivos y enlaces, selecciona **Navegador predeterminado** para que respete BrowserSwitch. El nombre o ubicación del ajuste puede variar con la versión y las políticas de tu organización. Esta migración no cambió la configuración de Teams.

## Actualizar

Usa **Salir de la bandeja**, cierra los selectores pendientes, actualiza el código y repite `Install.ps1` desde una consola normal permitida. Alternativamente compila y abre el ejecutable del repositorio desde el Explorador para pulsar **Instalar BrowserSwitch**. El destino local y el ProgID permanecen constantes, y las preferencias no se sustituyen. Comprueba un enlace externo después de actualizar.

Las copias `.previous` del ejecutable y de su `.config` permiten recuperar la versión previa después de cerrar la aplicación. `Data\Migration\registration-before.xml` conserva únicamente valores de registro propios previos a la instalación para diagnóstico o recuperación manual; no restaura asociaciones protegidas. No importes un destino antiguo si su ejecutable ya no existe.

## Desinstalar

1. Elige otro navegador en Configuración de Windows para HTTP, HTTPS, `.htm` y `.html`.
2. Cierra BrowserSwitch y ejecuta desde una consola normal permitida:

```powershell
.\scripts\Uninstall.ps1
# Opcional: eliminar también preferencias y respaldos personales
.\scripts\Uninstall.ps1 -RemovePersonalData
```

El script se niega a desregistrar mientras BrowserSwitch siga asignado a alguno de esos tipos. Solo elimina su registro HKCU, archivos del programa y accesos directos que apunten a la copia estable. Conserva `Data` salvo que indiques `-RemovePersonalData`. No toca otros navegadores ni las copias antiguas.

Si PowerShell está bloqueado, tras cambiar las asociaciones puedes ejecutar la aplicación permitida con `--unregister` y eliminar manualmente sus accesos directos y la carpeta local desde el Explorador. No modifiques políticas para desinstalar.

## Diagnóstico y verificación

```powershell
& "$env:LOCALAPPDATA\BrowserSwitch\BrowserSwitch.exe" --diagnostics "$env:TEMP\BrowserSwitch-diagnostic.xml"
```

El XML indica ruta ejecutada, configuración, estado HTTP/HTTPS y navegadores detectados. Contiene rutas personales: no lo subas al repositorio. La preparación deja un informe equivalente en `Data\install-check.xml`.

Consulta [la validación de esta migración](VALIDATION.md) para distinguir pruebas realizadas y pendientes. Para repetir el recorrido completo: activa Preguntar siempre, abre `https://example.com` desde una aplicación externa, elige un navegador, prueba recordar la elección, repite otro enlace y vuelve a Preguntar siempre.

Referencias de integración: [registro de capacidades por usuario](https://learn.microsoft.com/en-us/windows/win32/shell/default-programs), [página de aplicaciones predeterminadas](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-default-apps-settings), [reglas de argumentos de Windows](https://learn.microsoft.com/en-us/cpp/c-language/parsing-c-command-line-arguments).