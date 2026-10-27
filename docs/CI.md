# Comprobaciones automáticas de Windows

[Ver Windows CI en GitHub Actions](https://github.com/k-dennett/browser-switch/actions/workflows/windows.yml)

## Cuándo se ejecuta

- Al subir cambios a `main`.
- En los pull requests dirigidos a `main` (se comprueba el commit de integración).
- Manualmente, desde **Actions → Windows CI → Run workflow**, eligiendo la rama.

Cada ejecución usa una máquina de GitHub con Windows Server 2022 y .NET Framework 4.8 disponible. No instala BrowserSwitch en el equipo del usuario ni cambia sus asociaciones.

## Qué comprueba

El flujo ejecuta el mismo `scripts/Package.ps1` que se usa localmente. Ese script exige un checkout limpio e identifica el commit exacto; llama a Test.ps1, que compila el programa y ejecuta las pruebas.

La batería actual tiene **299 comprobaciones**: argumentos recibidos por un proceso real, validación de enlaces, preferencias, detección con entradas temporales HKCU y recursos/estilos nativos de Windows. Las entradas temporales del registro se limpian al terminar.

El empaquetador genera el ZIP, lo extrae en una ruta con espacios, compara todos los archivos y hashes, revisa la versión y ejecuta `--diagnostics` desde el binario extraído. Cualquier fallo detiene el trabajo y evita subir el paquete.

## Descargar el resultado

1. Abre una ejecución correcta de **Windows CI**.
2. En **Artifacts**, descarga **BrowserSwitch-windows-N**, donde N es el número de ejecución.
3. Extrae el archivo de artefactos de GitHub. Dentro están `BrowserSwitch-X.Y.Z-windows.zip` y `SHA256SUMS.txt`.
4. El ZIP interior es el paquete instalable: extrae todo su contenido y sigue LEEME.txt.

El resumen de cada ejecución muestra versión, commit, tamaño y SHA-256. Los artefactos se conservan **14 días**, sujetos a las políticas de GitHub. El repositorio es privado y su descarga requiere acceso.

Un paquete de CI identifica el commit probado: el número de versión solo cambia cuando se modifica AssemblyFileVersion. No equivale a una release nueva. Las [releases publicadas](https://github.com/k-dennett/browser-switch/releases/latest) siguen siendo la descarga estable y no se reemplazan desde este flujo.

## Permisos y mantenimiento

El workflow tiene `contents: read`, no conserva credenciales de checkout y utiliza únicamente el token temporal de GitHub. No necesita secretos personales ni ejecuta Publish-Release.ps1. Las acciones oficiales checkout y upload-artifact están fijadas a sus SHA de commit; sus comentarios indican la versión revisada.

El trabajo tiene un límite de diez minutos. Una ejecución nueva de la misma rama o pull request cancela la anterior. El runner Windows y los artefactos consumen la cuota del repositorio; no se configuraron compras ni runners propios.

## Qué sigue pendiente

Un resultado verde valida la construcción, las pruebas y el paquete en el runner. Sigue siendo necesario probar en otro Windows la instalación sin administrador, las asociaciones mediante Configuración, los enlaces desde otras aplicaciones, la bandeja, el inicio tras entrar a sesión, la actualización y la desinstalación completas.

Windows Server del runner no sustituye una sesión interactiva de Windows 10/11 ni reproduce las políticas particulares de cada organización.
