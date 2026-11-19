# BrowserSwitch para Ubuntu con GNOME

BrowserSwitch aparece como candidato a navegador predeterminado. Una vez que lo eliges en GNOME, el icono permite fijar **Firefox**, **Google Chrome** o **Chromium**, o activar **Preguntar siempre**. También puedes cambiar la preferencia abriendo **BrowserSwitch** desde el menú de aplicaciones.

## Dependencias y paquete

- Ubuntu 24.04 LTS como base del prototipo. Para ejecutar el paquete se necesita **.NET Runtime 10**, GTK 3 (`libgtk-3-0t64`), GLib/GIO y el bus D-Bus de la sesión. El instalador requiere `python3` y `desktop-file-utils` para registrar las asociaciones en la caché de aplicaciones.
- Para el icono se necesita `libayatana-appindicator3-1` y la extensión **Ubuntu AppIndicators** o **AppIndicator and KStatusNotifierItem Support** activa en GNOME. La ventana de preferencias funciona aunque falte la biblioteca o la extensión. [Extensión de Ubuntu](https://github.com/ubuntu/gnome-shell-extension-appindicator)
- Instala al menos uno de los navegadores compatibles. Se buscan sus comandos en el `PATH` de la sesión y en `/snap/bin`. Esto cubre los lanzadores habituales de paquetes Debian y Snap.

El prototipo usa GTK 3 para las ventanas y Ayatana AppIndicator para exportar el menú por D-Bus. El paquete depende del runtime y las bibliotecas del sistema. Contiene el ensamblado administrado con los modelos y la validación de `BrowserSwitch.Core`, el icono y el instalador. El archivo `BrowserSwitch-linux.tar.gz` generado mide **20,1 KiB (20 560 bytes)**. La solución de Windows sigue compilando por separado.

## Compilar e instalar

Con el **SDK .NET 10** disponible en `PATH`, ejecuta desde la raíz del repositorio:

```bash
bash scripts/package-linux.sh
tar -xzf artifacts/BrowserSwitch-linux.tar.gz -C artifacts
python3 artifacts/BrowserSwitch-linux/install.py install
```

La instalación de BrowserSwitch usa las carpetas XDG de tu usuario y funciona sin `sudo`. El instalador busca `dotnet` en `PATH` y en `~/.dotnet`. Microsoft documenta cómo instalar el SDK o el runtime en Ubuntu, incluida la instalación en el directorio personal. [Guía de .NET para Ubuntu](https://learn.microsoft.com/es-es/dotnet/core/install/linux-ubuntu-install?pivots=os-linux-ubuntu-2404)

Abre **BrowserSwitch** desde el menú de aplicaciones para mostrar las preferencias y arrancar el icono. En los siguientes inicios de sesión, GNOME ejecutará la entrada de autoarranque. En **Configuración → Aplicaciones → Aplicaciones predeterminadas**, elige **BrowserSwitch** en la opción **Web** para dirigirle HTTP y HTTPS. [Guía de Ubuntu](https://help.ubuntu.com/stable/ubuntu-help/net-default-browser.html.es)

## Probar el flujo en GNOME

1. Elige **Firefox** en el menú del icono. Abre `https://example.org/?paso=1` desde otra aplicación, como un documento con un hipervínculo, y comprueba que llega a Firefox.
2. Elige **Google Chrome** o **Chromium** desde el icono. Abre `https://example.org/?paso=2` desde la misma aplicación y comprueba el nuevo destino.
3. Activa **Preguntar siempre** y abre otro enlace. Debe aparecer el selector. Elige un navegador para ese enlace y repite la apertura para comprobar que se vuelve a preguntar.
4. Sal desde el menú del icono y vuelve a abrir BrowserSwitch para comprobar que conservó la preferencia. Si el icono está oculto, cambia la preferencia desde la ventana del menú de aplicaciones y repite la apertura.

También puedes probar las asociaciones con `gio open 'https://example.org/?paso=1'` y comprobarlas con `gio mime x-scheme-handler/http` y `gio mime x-scheme-handler/https`.

## Desinstalar

```bash
python3 "${XDG_DATA_HOME:-$HOME/.local/share}/browser-switch/install.py" uninstall
```

El instalador retira el acceso del menú, el autoarranque y las referencias a BrowserSwitch en los archivos de asociaciones del usuario. Se conservan las demás entradas. Si BrowserSwitch era el predeterminado, GNOME puede usar otra asociación disponible o puedes elegir otro navegador desde Configuración. La preferencia de BrowserSwitch se conserva en `${XDG_CONFIG_HOME:-$HOME/.config}/browser-switch/preferred-browser`.

## Validación del prototipo

- **Ubuntu 24.04 en WSLg.** Se compiló el paquete con .NET SDK 10.0.404. Se validaron la entrada de aplicación y la de autoarranque con `desktop-file-validate`. En un entorno XDG aislado, GIO mostró BrowserSwitch como candidato para HTTP y HTTPS y conservó el navegador predeterminado durante la instalación. Con la utilidad de caché ausente, el instalador indicó el requisito antes de escribir archivos.
- **Menú y ventanas.** Con un anfitrión AppIndicator simulado, se accionó el menú exportado por D-Bus. En Wayland y X11 se abrieron enlaces con `gio open` y ejecutables de prueba para Firefox, Chrome y Chromium. Cada ejecutable recibió la URL como un solo argumento. Se comprobó la selección puntual y su cancelación, el selector cuando desaparece el destino fijo, el cambio de preferencia desde el menú de aplicaciones sin la biblioteca de bandeja y la persistencia tras reiniciar el proceso.
- **Desinstalación.** Se verificó que termina la bandeja y limpia las referencias a BrowserSwitch para ambos protocolos, conserva las entradas de otros navegadores y permite que GIO vuelva a resolver HTTP y HTTPS con el navegador alternativo.
- **Windows.** Las **327 comprobaciones** de `scripts/Test.ps1` pasaron. `BrowserSwitch.sln` compiló con MSBuild en una copia temporal local. El MSBuild del equipo emitió MSB3644 y MSB3270 al resolver las referencias de .NET Framework 4.0 desde el entorno instalado.
- **Pendiente en GNOME.** Queda por confirmar el icono en el panel, el autoarranque al iniciar sesión y el recorrido con enlaces de otra aplicación y navegadores reales en una sesión completa de Ubuntu.
